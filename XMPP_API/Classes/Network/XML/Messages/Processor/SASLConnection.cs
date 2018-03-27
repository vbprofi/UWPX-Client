﻿using Logging;
using System;
using System.Collections;
using System.Threading.Tasks;
using XMPP_API.Classes.Network.Events;
using XMPP_API.Classes.Network.TCP;
using XMPP_API.Classes.Network.XML.Messages.Features;
using XMPP_API.Classes.Network.XML.Messages.Features.SASL;

namespace XMPP_API.Classes.Network.XML.Messages.Processor
{
    class SASLConnection : AbstractMessageProcessor
    {
        //--------------------------------------------------------Attributes:-----------------------------------------------------------------\\
        #region --Attributes--
        private SASLState state;
        private AbstractSASLMechanism selectedMechanism;
        private static readonly ArrayList OFFERED_MECHANISMS = new ArrayList() { "plain", "scram-sha-1", "digest-md5", "x-oauth2" };

        #endregion
        //--------------------------------------------------------Constructor:----------------------------------------------------------------\\
        #region --Constructors--
        /// <summary>
        /// Basic Constructor
        /// </summary>
        /// <history>
        /// 22/08/2017 Created [Fabian Sauter]
        /// </history>
        public SASLConnection(TCPConnection tcpConnection, XMPPConnection xMPPConnection) : base(tcpConnection, xMPPConnection)
        {
            reset();
        }

        #endregion
        //--------------------------------------------------------Set-, Get- Methods:---------------------------------------------------------\\
        #region --Set-, Get- Methods--
        private ArrayList getMechanisms(StreamFeaturesMessage msg)
        {
            foreach (StreamFeature sF in msg.getFeatures())
            {
                if (sF is SASLStreamFeature)
                {
                    return (sF as SASLStreamFeature).getMechanisms();
                }
            }
            return null;
        }

        #endregion
        //--------------------------------------------------------Misc Methods:---------------------------------------------------------------\\
        #region --Misc Methods (Public)--
        public override void reset()
        {
            state = SASLState.DISCONNECTED;
            selectedMechanism = null;
        }

        #endregion

        #region --Misc Methods (Private)--
        private void selectMechanism(ArrayList mechanisms)
        {
            string selected = null;
            foreach (string s in OFFERED_MECHANISMS)
            {
                foreach (string m in mechanisms)
                {
                    if (m.ToLower().Equals(s))
                    {
                        selected = s;
                        break;
                    }
                }
                if (selected != null)
                {
                    break;
                }
            }
            XMPPAccount sCC = XMPP_CONNECTION.account;
            switch (selected)
            {
                case "scram-sha-1":
                    selectedMechanism = new ScramSha1SASLMechanism(sCC.user.userId, sCC.user.userPassword);
                    break;
                case "digest-md5":
                    break;
                case "x-oauth2":
                    break;
                case "plain":
                    selectedMechanism = new PlainSASLMechanism(sCC.user.userId, sCC.user.userPassword);
                    break;
            }
        }

        #endregion

        #region --Misc Methods (Protected)--
        protected async override Task processMessageAsync(NewValidMessageEventArgs args)
        {
            AbstractMessage msg = args.getMessage();
            if (state == SASLState.CONNECTED || msg.isProcessed())
            {
                return;
            }

            switch (state)
            {
                case SASLState.DISCONNECTED:
                    if (msg is StreamFeaturesMessage || msg is OpenStreamAnswerMessage)
                    {
                        StreamFeaturesMessage features = null;
                        if (msg is OpenStreamAnswerMessage)
                        {
                            features = (msg as OpenStreamAnswerMessage).getStreamFeaturesMessage();
                        }
                        else
                        {
                            features = msg as StreamFeaturesMessage;
                        }

                        if (features == null)
                        {
                            return;
                        }

                        ArrayList mechanisms = getMechanisms(features);
                        if (mechanisms == null)
                        {
                            return;
                        }
                        setMessageProcessed(args);
                        selectMechanism(mechanisms);
                        if (selectedMechanism == null)
                        {
                            state = SASLState.ERROR;
                            await XMPP_CONNECTION.onMessageProcessorFailedAsync("selectedMechanism == null", true);
                            return;
                        }
                        await XMPP_CONNECTION.sendAsync(selectedMechanism.getSelectSASLMechanismMessage(), false, true);
                        state = SASLState.REQUESTED;
                    }
                    break;
                case SASLState.REQUESTED:
                case SASLState.CHALLENGING:
                    if (msg is ScramSha1ChallengeMessage)
                    {
                        state = SASLState.CHALLENGING;
                        setMessageProcessed(args);
                        await XMPP_CONNECTION.sendAsync(selectedMechanism.generateResponse(msg), false, true);
                    }
                    else if (msg is SASLSuccessMessage)
                    {
                        state = SASLState.CONNECTED;
                        msg.setRestartConnection(AbstractMessage.SOFT_RESTART);
                        setMessageProcessed(args);
                    }
                    else if (msg is SASLFailureMessage)
                    {
                        SASLFailureMessage saslFailureMessage = msg as SASLFailureMessage;
                        Logger.Error("Error during SASL authentication: " + saslFailureMessage.ERROR_MESSAGE);
                        state = SASLState.ERROR;
                        await XMPP_CONNECTION.onMessageProcessorFailedAsync("SASL: " + saslFailureMessage.ERROR_MESSAGE, true);
                    }
                    break;
                case SASLState.CONNECTED:
                    break;
                default:
                    throw new InvalidOperationException("Invalid state for message!" + state);
            }

        }

        #endregion
        //--------------------------------------------------------Events:---------------------------------------------------------------------\\
        #region --Events--


        #endregion
    }
}
