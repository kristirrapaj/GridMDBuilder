using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridConfig.Interface
{
    public class GridColumnConfigCustom
    {
        public string DATA_FIELD_NAME;
        public string CAPTION;
        public bool VISIBLE;
        public bool DISABLED;
        public int POSITION;

        /// <summary>
        /// Costruttore vuoto per deserializzazione
        /// </summary>
        public GridColumnConfigCustom()
        {
            
        }

        public GridColumnConfigCustom(GridColumnConfig columnConfig)
        {
            DATA_FIELD_NAME = columnConfig.DATA_FIELD_NAME;
            CAPTION = columnConfig.CAPTION;
            VISIBLE = columnConfig.VISIBLE;
            DISABLED = columnConfig.DISABLED;
            POSITION = columnConfig.POSITION;
        }
    }
}
