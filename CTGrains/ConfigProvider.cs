using CTGrainInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrains
{
    class ConfigProvider : IConfigReader, IConfigWriter
    {
        volatile ReadOnlyConfigData ConfigData;
        public int Version => ConfigData?.Version ?? int.MinValue;

        ReadOnlyConfigData IConfigReader.Config => ConfigData;

        ConfigData IConfigWriter.Config
        {
            set
            {
                if (value != null)
                    ConfigData = new ReadOnlyConfigData(value);
            }
        }
    }
}
