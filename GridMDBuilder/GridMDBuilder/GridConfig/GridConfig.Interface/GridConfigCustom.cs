using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridConfig.Interface
{
    public class GridConfigCustom
    {
        public string ConfigName;

        public List<GridColumnConfigCustom> ColumnsConfig = new List<GridColumnConfigCustom>();

        /// <summary>
        /// Costruttore vuoto per deserializzazione.
        /// </summary>
        public GridConfigCustom()
        {

        }

        public GridConfigCustom(GridConfig gridConfig)
        {
            ConfigName = gridConfig.ConfigName;

            foreach (GridColumnConfig columnConfig in gridConfig.ColumnsConfig)
            {
                ColumnsConfig.Add(new GridColumnConfigCustom(columnConfig));
            }
        }

    }

}
