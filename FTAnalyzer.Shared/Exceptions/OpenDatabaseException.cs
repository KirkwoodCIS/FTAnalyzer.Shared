﻿using System;

namespace FTAnalyzer
{
    [Serializable]
    public class OpenDatabaseException : Exception
    {
        public OpenDatabaseException(string message)
            : base(message)
        { }

        public OpenDatabaseException()
        {
        }

        public OpenDatabaseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
