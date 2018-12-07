using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using LMLogLevel = LightMessage.Common.Util.LogLevel;

namespace CTHost
{
    class LightMessageLogProvider : LightMessage.Common.Util.ILogProvider
    {
        ILogger logger;
        LMLogLevel level;
#if DEBUG
        bool logVerboseAsInformation;
#endif

        Dictionary<LMLogLevel, LogLevel> levelLookup = new Dictionary<LMLogLevel, LogLevel>
        {
            { LMLogLevel.None, LogLevel.None },
            { LMLogLevel.Verbose, LogLevel.Debug },
            { LMLogLevel.Info, LogLevel.Information },
            { LMLogLevel.Warning, LogLevel.Warning },
            { LMLogLevel.Error, LogLevel.Error },
        };

        public LightMessageLogProvider(ILogger logger, LMLogLevel level
#if DEBUG
            , bool logVerboseAsInformation
#endif
            )
        {
            this.logger = logger;
            this.level = level;
#if DEBUG
            this.logVerboseAsInformation = logVerboseAsInformation;
#endif
        }

        public LMLogLevel GetLevel()
        {
            return level;
        }

        public void Log(string Text, LMLogLevel Level)
        {
#if DEBUG
            if (logVerboseAsInformation && Level == LMLogLevel.Verbose)
            {
                logger.Log(LogLevel.Information, Text);
                return;
            }
#endif
            logger.Log(levelLookup[Level], Text);
        }
    }
}
