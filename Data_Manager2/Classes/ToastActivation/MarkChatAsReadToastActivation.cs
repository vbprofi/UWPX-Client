﻿namespace Data_Manager2.Classes.ToastActivation
{
    public class MarkChatAsReadToastActivation : AbstractToastActivation
    {
        //--------------------------------------------------------Attributes:-----------------------------------------------------------------\\
        #region --Attributes--
        public const string TYPE = "MARK_CHAT_AS_READ";
        public readonly string CHAT_ID;

        #endregion
        //--------------------------------------------------------Constructor:----------------------------------------------------------------\\
        #region --Constructors--
        /// <summary>
        /// Basic Constructor
        /// </summary>
        /// <history>
        /// 25/08/2018 Created [Fabian Sauter]
        /// </history>
        public MarkChatAsReadToastActivation(string args, bool received)
        {
            this.CHAT_ID = args;
            this.IS_VALID = !string.IsNullOrEmpty(args);
        }

        #endregion
        //--------------------------------------------------------Set-, Get- Methods:---------------------------------------------------------\\
        #region --Set-, Get- Methods--


        #endregion
        //--------------------------------------------------------Misc Methods:---------------------------------------------------------------\\
        #region --Misc Methods (Public)--
        public override string generate()
        {
            return TYPE + '=' + CHAT_ID;
        }

        #endregion

        #region --Misc Methods (Private)--


        #endregion

        #region --Misc Methods (Protected)--


        #endregion
        //--------------------------------------------------------Events:---------------------------------------------------------------------\\
        #region --Events--


        #endregion
    }
}
