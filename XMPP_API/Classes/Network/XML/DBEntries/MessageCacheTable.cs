﻿using SQLite;

namespace XMPP_API.Classes.Network.XML.DBEntries
{
    [Table(DBTableConsts.MESSAGE_CACHE_TABLE)]
    class MessageCacheTable
    {
        //--------------------------------------------------------Attributes:-----------------------------------------------------------------\\
        #region --Attributes--
        [PrimaryKey]
        public string messageId { get; set; }
        [NotNull]
        public string accountId { get; set; }
        [NotNull]
        public string message { get; set; }
        [NotNull]
        public bool isChatMessage { get; set; }
        public string chatMessageId { get; set; }

        #endregion
        //--------------------------------------------------------Constructor:----------------------------------------------------------------\\
        #region --Constructors--
        /// <summary>
        /// Basic Constructor
        /// </summary>
        /// <history>
        /// 26/09/2017 Created [Fabian Sauter]
        /// </history>
        public MessageCacheTable()
        {

        }

        #endregion
        //--------------------------------------------------------Set-, Get- Methods:---------------------------------------------------------\\
        #region --Set-, Get- Methods--


        #endregion
        //--------------------------------------------------------Misc Methods:---------------------------------------------------------------\\
        #region --Misc Methods (Public)--


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
