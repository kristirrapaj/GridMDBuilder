using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataAccess;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using DataAccess.Infrastructure;
using System.Data;

namespace GridConfig.Interface
{
    public interface IGridConfigManager
    {
        GridConfig GetGridConfig(string fieldsConfigName, string userId);

        List<GridColumnConfig> GetGridColConfigEx(string tbName, string fieldsConfigName, DataTable editingTable, Geometry spFilterGeometry);

        ColumnSettingsDS GetColSettings(string fieldsConfigName, string dataProviderName = null);

        List<GridColumnConfig> GetFieldsConfig(string fieldsConfigName);

        void ClearCache(string[] configNames = null);

        void ClearAllCache();
    }

}
