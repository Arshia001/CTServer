using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrains
{
    public class CTSettings
    {
        static CTSettings _Instance;
        public static CTSettings Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = JsonConvert.DeserializeObject<CTSettings>(File.ReadAllText("CTConfig.json"));
                return _Instance;
            }
        }


        public string ConnectionString { get; set; }


        private CTSettings() { }
    }
}
