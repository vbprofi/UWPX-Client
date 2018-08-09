﻿using libsignal;
using libsignal.state;
using Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XMPP_API.Classes.Network.XML.Messages;
using XMPP_API.Classes.Network.XML.Messages.XEP_0060;
using XMPP_API.Classes.Network.XML.Messages.XEP_0384;
using XMPP_API.Classes.Network.XML.Messages.XEP_0384.Signal;

namespace XMPP_API.Classes.Network
{
    public class OmemoHelper : IDisposable
    {
        //--------------------------------------------------------Attributes:-----------------------------------------------------------------\\
        #region --Attributes--
        public OmemoHelperState STATE { get; private set; }
        public OmemoDevices DEVICES { get; private set; }

        private readonly XMPPConnection2 CONNECTION;

        private uint tmpDeviceId;
        private MessageResponseHelper<IQMessage> requestDeviceListHelper;
        private MessageResponseHelper<IQMessage> updateDeviceListHelper;
        private MessageResponseHelper<IQMessage> announceBundleInfoHelper;

        // Keep sessions during App runtime:
        private readonly Dictionary<string, Tuple<SignalProtocolAddress, SessionBuilder>> SESSIONS_BUILDER;
        private readonly SessionStore SESSION_STORE;
        private readonly PreKeyStore PRE_KEY_STORE;
        private readonly SignedPreKeyStore SIGNED_PRE_KEY_STORE;
        private readonly IdentityKeyStore IDENTITY_STORE;

        #endregion
        //--------------------------------------------------------Constructor:----------------------------------------------------------------\\
        #region --Constructors--
        /// <summary>
        /// Basic Constructor
        /// </summary>
        /// <history>
        /// 06/08/2018 Created [Fabian Sauter]
        /// </history>
        public OmemoHelper(XMPPConnection2 connection)
        {
            this.CONNECTION = connection;

            this.SESSIONS_BUILDER = new Dictionary<string, Tuple<SignalProtocolAddress, SessionBuilder>>();
            this.SESSION_STORE = new OmemoSessionStore(connection.account);
            this.PRE_KEY_STORE = new OmemoPreKeyStore(connection.account);
            this.SIGNED_PRE_KEY_STORE = new OmemoSignedPreKeyStore(connection.account);
            this.IDENTITY_STORE = new OmemoIdentityKeyStore(connection.account);

            reset();
        }

        #endregion
        //--------------------------------------------------------Set-, Get- Methods:---------------------------------------------------------\\
        #region --Set-, Get- Methods--
        private void setState(OmemoHelperState newState)
        {
            if (STATE != newState)
            {
                OmemoHelperState oldState = STATE;
                STATE = newState;
                Logger.Debug("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") " + oldState + " -> " + STATE);
                if (STATE == OmemoHelperState.ERROR)
                {
                    CONNECTION.NewValidMessage -= CONNECTION_ConnectionNewValidMessage;

                }
                else if (oldState == OmemoHelperState.ERROR && STATE != OmemoHelperState.ERROR)
                {
                    reset();
                }

                if (STATE == OmemoHelperState.ENABLED)
                {
                    Logger.Info("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Enabled.");
                }
            }
        }

        public Tuple<SignalProtocolAddress, SessionBuilder> getSession(string name)
        {
            if (SESSIONS_BUILDER.ContainsKey(name))
            {
                return SESSIONS_BUILDER[name];
            }
            return null;
        }

        public SessionCipher getSessionCipher(SignalProtocolAddress remoteAddress)
        {
            if (SESSIONS_BUILDER.ContainsKey(remoteAddress.getName()))
            {
                return new SessionCipher(SESSION_STORE, PRE_KEY_STORE, SIGNED_PRE_KEY_STORE, IDENTITY_STORE, remoteAddress);
            }
            return null;
        }

        #endregion
        //--------------------------------------------------------Misc Methods:---------------------------------------------------------------\\
        #region --Misc Methods (Public)--
        public void Dispose()
        {
            CONNECTION.ConnectionStateChanged -= CONNECTION_ConnectionStateChanged;
            CONNECTION.NewValidMessage -= CONNECTION_ConnectionNewValidMessage;
        }

        public void reset()
        {
            setState(OmemoHelperState.DISABLED);

            CONNECTION.ConnectionStateChanged -= CONNECTION_ConnectionStateChanged;
            CONNECTION.ConnectionStateChanged += CONNECTION_ConnectionStateChanged;

            CONNECTION.NewValidMessage -= CONNECTION_ConnectionNewValidMessage;
            CONNECTION.NewValidMessage += CONNECTION_ConnectionNewValidMessage;

            tmpDeviceId = 0;

            if (requestDeviceListHelper != null)
            {
                requestDeviceListHelper.Dispose();
                requestDeviceListHelper = null;
            }
            if (updateDeviceListHelper != null)
            {
                updateDeviceListHelper.Dispose();
                updateDeviceListHelper = null;
            }
            if (announceBundleInfoHelper != null)
            {
                announceBundleInfoHelper.Dispose();
                announceBundleInfoHelper = null;
            }
        }

        public SessionBuilder newSession(string chatJid, uint recipientDeviceId, PreKeyBundle recipientPreKey)
        {
            SignalProtocolAddress address = new SignalProtocolAddress(chatJid, recipientDeviceId);
            SessionBuilder builder = new SessionBuilder(SESSION_STORE, PRE_KEY_STORE, SIGNED_PRE_KEY_STORE, IDENTITY_STORE, address);
            builder.process(recipientPreKey);
            SESSIONS_BUILDER[chatJid] = new Tuple<SignalProtocolAddress, SessionBuilder>(address, builder);
            return builder;
        }

        #endregion

        #region --Misc Methods (Private)--
        private void requestDeviceList()
        {
            setState(OmemoHelperState.REQUESTING_DEVICE_LIST);
            Logger.Info("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Requesting device list.");
            if (requestDeviceListHelper != null)
            {
                requestDeviceListHelper.Dispose();
            }
            requestDeviceListHelper = new MessageResponseHelper<IQMessage>(CONNECTION, onRequestDeviceListMsg, onTimeout);
            OmemoRequestDeviceListMessage msg = new OmemoRequestDeviceListMessage(CONNECTION.account.getIdDomainAndResource(), null);
            requestDeviceListHelper.start(msg);
        }

        private bool onRequestDeviceListMsg(AbstractMessage msg)
        {
            if (msg is OmemoDeviceListResultMessage devMsg)
            {
                updateDevicesIfNeeded(devMsg.DEVICES);
                return true;
            }
            else if (msg is IQErrorMessage errMsg)
            {
                if (errMsg.ERROR_OBJ.ERROR_NAME == ErrorName.ITEM_NOT_FOUND)
                {
                    Logger.Warn("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Failed to request OMEMO device list - node does not exist. Creating node.");
                    updateDevicesIfNeeded(null);
                }
                else
                {
                    Logger.Error("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Failed to request OMEMO device list form: " + CONNECTION.account.user.domain + "\n" + errMsg.ERROR_OBJ.ToString());
                    setState(OmemoHelperState.ERROR);
                }
                return true;
            }
            return false;
        }

        private void announceBundleInfo()
        {
            setState(OmemoHelperState.ANNOUNCING_BUNDLE_INFO);
            Logger.Info("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Announcing bundle information for: " + CONNECTION.account.omemoDeviceId);
            if (announceBundleInfoHelper != null)
            {
                announceBundleInfoHelper.Dispose();
            }
            announceBundleInfoHelper = new MessageResponseHelper<IQMessage>(CONNECTION, announceBundleInfoMsg, onTimeout);
            OmemoSetBundleInformationMessage msg = new OmemoSetBundleInformationMessage(CONNECTION.account.getIdDomainAndResource(), CONNECTION.account.getOmemoBundleInformation(), CONNECTION.account.omemoDeviceId);
            announceBundleInfoHelper.start(msg);
        }

        private bool announceBundleInfoMsg(AbstractMessage msg)
        {
            if (msg is IQErrorMessage errMsg)
            {
                Logger.Error("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Failed to announce OMEMO bundle info to: " + CONNECTION.account.user.domain + "\n" + errMsg.ERROR_OBJ.ToString());
                setState(OmemoHelperState.ERROR);
                return true;
            }
            else if (msg is IQMessage)
            {
                Logger.Info("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Bundle info announced.");
                CONNECTION.account.omemoBundleInfoAnnounced = true;
                CONNECTION.account.onPropertyChanged(nameof(CONNECTION.account.omemoBundleInfoAnnounced));
                setState(OmemoHelperState.ENABLED);
                return true;
            }
            return false;
        }

        private void updateDevicesIfNeeded(OmemoDevices devicesRemote)
        {
            if (devicesRemote == null)
            {
                devicesRemote = new OmemoDevices();
            }

            bool updateDeviceList = false;
            // Device id hasn't been set. Pick a random, unique one:
            if (CONNECTION.account.omemoDeviceId == 0)
            {
                tmpDeviceId = OmemoUtils.generateDeviceId(devicesRemote.DEVICES);
                devicesRemote.DEVICES.Add(tmpDeviceId);
                updateDeviceList = true;
            }
            else
            {
                if (!devicesRemote.DEVICES.Contains(CONNECTION.account.omemoDeviceId))
                {
                    devicesRemote.DEVICES.Add(CONNECTION.account.omemoDeviceId);
                    updateDeviceList = true;
                }
            }

            if (updateDeviceList)
            {
                setState(OmemoHelperState.UPDATING_DEVICE_LIST);
                Logger.Info("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Updating device list.");
                if (updateDeviceListHelper != null)
                {
                    updateDeviceListHelper.Dispose();
                }
                updateDeviceListHelper = new MessageResponseHelper<IQMessage>(CONNECTION, updateDevicesIfNeededMsg, onTimeout);
                OmemoSetDeviceListMessage msg = new OmemoSetDeviceListMessage(CONNECTION.account.getIdDomainAndResource(), devicesRemote);
                updateDeviceListHelper.start(msg);
            }
            else if (!CONNECTION.account.omemoBundleInfoAnnounced)
            {
                announceBundleInfo();
            }
            else
            {
                setState(OmemoHelperState.ENABLED);
            }
            DEVICES = devicesRemote;
        }

        private bool updateDevicesIfNeededMsg(AbstractMessage msg)
        {
            if (msg is IQErrorMessage errMsg)
            {
                Logger.Error("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Failed to set OMEMO device list to: " + CONNECTION.account.user.domain + "\n" + errMsg.ERROR_OBJ.ToString());
                setState(OmemoHelperState.ERROR);
                return true;
            }
            else if (msg is IQMessage)
            {
                Logger.Info("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Device list updated.");
                if (CONNECTION.account.omemoDeviceId == 0)
                {
                    CONNECTION.account.omemoDeviceId = tmpDeviceId;
                    CONNECTION.account.onPropertyChanged(nameof(CONNECTION.account.omemoDeviceId));
                }
                if (!CONNECTION.account.omemoBundleInfoAnnounced)
                {
                    announceBundleInfo();
                }
                else
                {
                    setState(OmemoHelperState.ENABLED);
                }
                return true;
            }
            return false;
        }

        private void onTimeout()
        {
            switch (STATE)
            {
                case OmemoHelperState.REQUESTING_DEVICE_LIST:
                case OmemoHelperState.UPDATING_DEVICE_LIST:
                case OmemoHelperState.ANNOUNCING_BUNDLE_INFO:
                    Logger.Error("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Failed in state " + STATE + " - timeout!");
                    setState(OmemoHelperState.ERROR);
                    break;
            }
        }

        /// <summary>
        /// If the OMEMO device list got changed and does not contain the local device id update it and add it again.
        /// </summary>
        /// <param name="msg">The received OmemoDeviceListEventMessage.</param>
        private async Task onOmemoDeviceListEventMessageAsync(OmemoDeviceListEventMessage msg)
        {
            if (!msg.DEVICES.DEVICES.Contains(CONNECTION.account.omemoDeviceId))
            {
                msg.DEVICES.DEVICES.Add(CONNECTION.account.omemoDeviceId);
                OmemoSetDeviceListMessage setMsg = new OmemoSetDeviceListMessage(CONNECTION.account.getIdDomainAndResource(), msg.DEVICES);
                await CONNECTION.sendAsync(setMsg, false, false);
            }
        }

        #endregion

        #region --Misc Methods (Protected)--


        #endregion
        //--------------------------------------------------------Events:---------------------------------------------------------------------\\
        #region --Events--
        private async void CONNECTION_ConnectionNewValidMessage(IMessageSender sender, Events.NewValidMessageEventArgs args)
        {
            if (args.MESSAGE is OmemoDeviceListEventMessage eventMsg)
            {
                await onOmemoDeviceListEventMessageAsync(eventMsg);
            }
        }

        private void CONNECTION_ConnectionStateChanged(AbstractConnection2 connection, Events.ConnectionStateChangedEventArgs arg)
        {
            switch (arg.newState)
            {
                case ConnectionState.CONNECTED:
                    if (!CONNECTION.account.hasOmemoKeys())
                    {
                        setState(OmemoHelperState.ERROR);
                        Logger.Error("[OMEMO HELPER](" + CONNECTION.account.getIdAndDomain() + ") Failed - no keys!");
                    }
                    else if (STATE == OmemoHelperState.DISABLED)
                    {
                        requestDeviceList();
                    }
                    break;

                case ConnectionState.DISCONNECTED:
                case ConnectionState.ERROR:
                    reset();
                    break;
            }
        }

        #endregion
    }
}
