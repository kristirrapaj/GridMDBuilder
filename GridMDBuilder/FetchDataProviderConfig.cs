using System;
using System.Data;
using System.IO;
using DataStore.Interface;

namespace GridMDBuilder
{
    public class FetchDataProviderConfig
    {
        private static string configPath = "C:\\Users\\K.Rrapaj\\Desktop\\PROJECTS\\SWMS\\STMapClient\\src-core\\Server\\GridConfig\\GridMDBuilder\\GridMDBuilder";
        public static DataStoreConfig InitDataStoreConfig()
        {
            DataStoreConfig mDataStoreConfig = new DataStoreConfig();
            DataSet ds = new DataSet();
            
            string file = Directory.GetFiles(configPath, "*.xml")[0];
            ds.ReadXml(file);
            
            mDataStoreConfig.connStr = ds.Tables[0].Rows[0]["CONN_STR"].ToString();
            mDataStoreConfig.schema = ds.Tables[0].Rows[0]["SCHEMA"].ToString();
            mDataStoreConfig.sdeDefaultVersion = ds.Tables[0].Rows[0]["SDE_DEFAULT_VERSION"].ToString();
            mDataStoreConfig.srid = int.Parse(ds.Tables[0].Rows[0]["SRID"].ToString());
            mDataStoreConfig.idField = ds.Tables[0].Rows[0]["ID_FIELD"].ToString();
            mDataStoreConfig.shapeField = ds.Tables[0].Rows[0]["SHAPE_FIELD"].ToString();

            return mDataStoreConfig;
        }
    }
}