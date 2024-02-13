using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataAccess;
using DataAccessManager;
using GeoAPI.Geometries;
using GridConfig.Interface;
using InfrastructureData.DTO;
using MapConfig.Interface;
using NetTopologySuite.Geometries;
using DataAccess.Infrastructure;
using Infrastructure.DataUtils;
using DomainManager.Interface;
using SubTypeManager.Interface;
using ConfigManagement.Interface;
using DataStore.Interface;
using Session.Interface;
using Infrastructure.STLogging.Factory;
using System.Text.Json;

namespace GridConfig
{
    public class GridConfigManager : IGridConfigManager
    {
        #region Campi privati

        EntityManager mEntityManager;
        private string mConfigBasePath;
        private IMapConfigManager mMapConfigManager;

        private static Dictionary<string, Interface.GridConfig> mGridConfigs = new Dictionary<string, Interface.GridConfig>();
        private static Dictionary<string, ColumnSettingsDS> mColumnSettings = new Dictionary<string, ColumnSettingsDS>();
        private static Dictionary<string, List<GridColumnConfig>> mFieldsConfig = new Dictionary<string, List<GridColumnConfig>>();

        private IDomainManager mDomainManager;
        private ISubTypeManager mSubTypeManager;

        private IConfigManager mConfigManager;
        protected ISessionManager mSessionManager;

        #endregion Campi privati

        public GridConfigManager(EntityManager entityManager, string configBasePath, IMapConfigManager mapConfigManager
            , IDomainManager domainManager, ISubTypeManager subTypeManager, IConfigManager configManager, ISessionManager sessionManager)
        {
            mEntityManager = entityManager;

            mConfigBasePath = configBasePath;

            mMapConfigManager = mapConfigManager;

            mDomainManager = domainManager;
            mSubTypeManager = subTypeManager;

            mConfigManager = configManager;
            mSessionManager = sessionManager;
        }

        public Interface.GridConfig GetGridConfig(string fieldsConfigName, string userId)
        {
            lock (GridConfigManager.mGridConfigs)
            {
                Interface.GridConfig gc = null;

                if (GridConfigManager.mGridConfigs.ContainsKey(fieldsConfigName))
                {
                    gc = GridConfigManager.mGridConfigs[fieldsConfigName];
                }
                else
                {
                    gc = BuildGridConfig(fieldsConfigName);

                    if (gc.UseCache)
                    {
                        GridConfigManager.mGridConfigs[fieldsConfigName] = gc;
                    }
                }

                // vado a lavorare su una copia, altrimenti vado a sporcare la configurazione di base cacheata.
                gc = gc.Clone();

                SetDefaultDisabledColumns(gc);
                SetDefaultNonEditableColumns(gc);

                if (userId != null)
                {

                    string key = GetGridConfigKey() + fieldsConfigName;

                    // customizzo la configurazione a partire da quella del profilo dell'utente.
                    string value = mConfigManager.GetUserProfileConfigValue(userId, key);

                    if (value != null)
                    {
                        MergeWithSettings(gc, value);
                    }

                    // poi customizzo a partire dai settings dell'utente.
                    value = mConfigManager.GetUserConfigValue(userId, key);

                    if (value != null)
                    {
                        MergeWithSettings(gc, value);
                    }
                }

                return gc;
            }
        }

        private void SetDefaultDisabledColumns(Interface.GridConfig gc)
        {
            string disabledColumns = ConfigurationManager.AppSettings["DISABLED_COLUMNS"];

            if (disabledColumns != null)
            {
                string[] disabledColsArr = disabledColumns.Split(';');

                foreach (GridColumnConfig colConfig in gc.ColumnsConfig)
                {
                    if (disabledColsArr.Contains(colConfig.DATA_FIELD_NAME))
                    {
                        colConfig.DISABLED = true;
                    }
                }
            }
        }

        private void SetDefaultNonEditableColumns(Interface.GridConfig gc)
        {
            string nonEditableColumns = ConfigurationManager.AppSettings["NON_EDITABLE_COLUMNS"];

            if (nonEditableColumns != null)
            {
                string[] nonEditableColsArr = nonEditableColumns.Split(';');

                foreach (GridColumnConfig colConfig in gc.ColumnsConfig)
                {
                    if (nonEditableColsArr.Contains(colConfig.DATA_FIELD_NAME))
                    {
                        colConfig.EDITABLE = false;
                    }
                }
            }
        }

        private void MergeWithSettings(Interface.GridConfig gc, string value)
        {
            GridConfigCustom gcCustom = JsonSerializer.Deserialize<GridConfigCustom>(value, Utility.Serialization.DefaultJsonDeserializationOptions);

            foreach (GridColumnConfigCustom customColConfig in gcCustom.ColumnsConfig)
            {
                GridColumnConfig colConfig = gc.ColumnsConfig.Find((cc) => { return cc.DATA_FIELD_NAME == customColConfig.DATA_FIELD_NAME; });

                if (colConfig != null)
                {
                    colConfig.VISIBLE = customColConfig.VISIBLE;
                    colConfig.POSITION = customColConfig.POSITION;
                    colConfig.DISABLED = customColConfig.DISABLED;
                }
            }
        }

        private string GetGridConfigKey()
        {
            string appName = ConfigurationManager.AppSettings["APP_NAME"];

            string key = $"{appName}.Grid.";

            return key;
        }


        public List<GridColumnConfig> GetGridColConfigEx(string tbName, string configName, DataTable editingTable, Geometry spFilterGeometry)
        {
            List<GridColumnConfig> lstColConfigs = new List<GridColumnConfig>();

            Interface.GridConfig gc = GetGridConfig(configName, null);

            ColumnSettingsDS ds = GetColSettings(configName);
            
            Dictionary<string, Dictionary<string, DefValueGeomDepParams>> dvpDict = new Dictionary<string, Dictionary<string, DefValueGeomDepParams>>();

            //Vediamo se ci sono i parametri per prendere il valore di default dagli oggetti vicini
            //Se ci sono li raggruppo per tabella in modo da fare una query sola pr tutti i campi che devono prendere il valore dalla stessa tabella
            foreach (GridColumnConfig colConfig in gc.ColumnsConfig)
            {
                if (!string.IsNullOrEmpty(colConfig.DEF_VALUE_GEOM_DEP_PARAMS))
                {
                    DefValueGeomDepParams dvParams = JsonSerializer.Deserialize<DefValueGeomDepParams>(colConfig.DEF_VALUE_GEOM_DEP_PARAMS, Utility.Serialization.DefaultJsonDeserializationOptions);

                    string key = (dvParams.DATA_PROVIDER_NAME == null ? "" : dvParams.DATA_PROVIDER_NAME) + "." + dvParams.SRC_TABLE;

                    if (!dvpDict.ContainsKey(key))
                    {
                        dvpDict.Add(key, new Dictionary<string, DefValueGeomDepParams>());
                    }

                    dvpDict[key].Add(colConfig.DATA_FIELD_NAME, dvParams);
                }
            }

            Dictionary<string, string> defValuesDict = new Dictionary<string, string>();

            foreach (Dictionary<string, DefValueGeomDepParams> dvpTblDict in dvpDict.Values)
            {
                Dictionary<string, string> tblDefValues = GetGeomDepDefaultValues(dvpTblDict, spFilterGeometry);

                foreach (string key in tblDefValues.Keys)
                {
                    defValuesDict.Add(key, tblDefValues[key]);
                }
            }


            bool hasToAddColConfig = false;

            Dictionary<string, Tuple<DataTypeEnum, object[]>> loadedDepLookupInfoValues = new Dictionary<string, Tuple<DataTypeEnum, object[]>>();

            foreach (GridColumnConfig colConfig in gc.ColumnsConfig)
            {
                if (colConfig.IS_LOOKUP && !colConfig.USE_AUTOCOMPLETE
                    && colConfig.LOOKUP_FILTER.StartsWith("GEOM_"))
                {
                    ColumnSettingsDS.COLUMN_SETTINGRow colSettingsRow = ds.COLUMN_SETTING.FindByDATA_FIELD_NAME(colConfig.DATA_FIELD_NAME);

                    if (colSettingsRow != null)
                    {
                        object editingId = null;

                        if (editingTable != null && editingTable.Rows.Count > 0 
                            && !editingTable.Rows[0].IsNull(colConfig.DATA_FIELD_NAME))
                        {
                            editingId = editingTable.Rows[0][colConfig.DATA_FIELD_NAME];
                        }
                        colConfig.LookupInfo = GetLookupInfo(colSettingsRow, spFilterGeometry, editingId: editingId);
                    }

                    ColumnSettingsDS.COLUMN_SETTINGRow[] depChildren = GetDepChildren(ds, colConfig.DATA_FIELD_NAME);

                    if (depChildren.Length > 0)
                    {
                        ////In questo caso carico già le dependencyInfo e le lookupInfo in un colpo solo
                        ////Tuple<List<LookupItem>, Dictionary<object, Dictionary<string, List<object>>>> depInfo = GetDependencyInfo(colConfig, depChildren, spFilterGeometry);
                        //Dictionary<object, Dictionary<string, List<object>>> depInfo = GetDependencyInfo(colConfig, depChildren, spFilterGeometry);
                        ///// colConfig.LookupInfo = depInfo.Item1;
                        //colConfig.DependencyDictionary = depInfo;

                        Dictionary <string,List<string>> depDict = GetDependencyFieldDict(depChildren);
                        colConfig.DependencyFieldsDict = depDict;

                        if (colConfig.LookupInfo != null && colConfig.LookupInfo.Count() > 0)
                        {
                            loadedDepLookupInfoValues.Add(colConfig.DATA_FIELD_NAME, new Tuple<DataTypeEnum, object[]>(ParseRuleDataType(colConfig.TYPE), colConfig.LookupInfo.Select((li) => li.Value).ToArray()));
                        }
                    }

                    hasToAddColConfig = true;
                }               

                //Vediamo se ci sono i parametri per prendere il valore di default dagli oggetti vicini
                if (defValuesDict.ContainsKey(colConfig.DATA_FIELD_NAME))
                {
                    colConfig.DEFAULT_VALUE = defValuesDict[colConfig.DATA_FIELD_NAME];

                    hasToAddColConfig = true;                    
                }

                if (hasToAddColConfig)
                {
                    lstColConfigs.Add(colConfig);
                }
            }

            //Aggiorno i dizionari delle dipendenze
            foreach (GridColumnConfig colConfig in gc.ColumnsConfig)
            {
                if (!string.IsNullOrEmpty(colConfig.DEPENDENCY_FIELD))
                {
                    string[] parentFields = colConfig.DEPENDENCY_FIELD.Split(';');

                    Dictionary<string, Tuple<DataTypeEnum, object[]>> parentValuesDict = new Dictionary<string, Tuple<DataTypeEnum, object[]>>();

                    foreach (string fld in parentFields)
                    {
                        if (loadedDepLookupInfoValues.ContainsKey(fld.ToUpper()))
                        {
                            parentValuesDict.Add(fld, loadedDepLookupInfoValues[fld]);
                        }
                    }

                    if (parentValuesDict.Count() > 0)
                    {
                        colConfig.DependencyDictionary = GetDependencyInfo(colConfig, parentValuesDict);
                    }

                    bool hasToLoad = (!lstColConfigs.Any((cc) => cc.DATA_FIELD_NAME == colConfig.DATA_FIELD_NAME)) ;
                    
                    if (hasToLoad)
                    {
                        lstColConfigs.Add(colConfig);
                    }
                }
            }

            return lstColConfigs;
        }

        public List<GridColumnConfig> GetFieldsConfig(string fieldsConfigName)
        {
            lock (GridConfigManager.mFieldsConfig)
            {
                List<GridColumnConfig> fc = null;

                if (GridConfigManager.mFieldsConfig.ContainsKey(fieldsConfigName))
                {
                    fc = GridConfigManager.mFieldsConfig[fieldsConfigName];
                }
                else
                {
                    fc = BuildFieldsConfig(fieldsConfigName);

                    GridConfigManager.mFieldsConfig[fieldsConfigName] = fc;
                }

                return fc;
            }
        }

        public ColumnSettingsDS GetColSettings(string fieldsConfigName, string dataProviderName = null)
        {
            lock (GridConfigManager.mColumnSettings)
            {
                ColumnSettingsDS ds = null;

                if (GridConfigManager.mColumnSettings.ContainsKey(fieldsConfigName))
                    ds = GridConfigManager.mColumnSettings[fieldsConfigName];
                else
                {
                    string configPath = Path.Combine(mConfigBasePath, fieldsConfigName + "Config.xml");

                    if (File.Exists(configPath))
                    {
                        ds = new ColumnSettingsDS();
                        ds.ReadXml(configPath);
                    }
                    else
                    {
                        STLoggerFactory.Logger.Log($"Impossibile trovare il file di configurazione {configPath}", Infrastructure.STLogging.Interface.LogLevelEnum.Warn);

                        // riempiere il dataset a partire dal db
                        IDatasetEntityManager entityManager = (string.IsNullOrEmpty(dataProviderName)) ? mEntityManager.DataEntityManager : DataFactory.Factory.GetDatasetEntityManager(dataProviderName, mSessionManager);

                        ds = CreateColumnSettingsDSFromDB(entityManager.DataStore, fieldsConfigName);

                        if (ds != null)
                        {
                            // salvare il dataset

                            bool save = false;
                            string saveOption = ConfigurationManager.AppSettings["SAVE_GENERATED_CONFIG"];
                            if (saveOption != null)
                            {
                                save = Boolean.Parse(saveOption);
                            }

                            if (save)
                            {
                                ds.WriteXml(configPath);
                            }
                        }
                        else
                        {
                            ds = new ColumnSettingsDS();
                        }
                        
                    }
                    
                    foreach (GridConfig.Interface.ColumnSettingsDS.COLUMN_SETTINGRow row in ds.COLUMN_SETTING)
                    {
                        // E' necessario per uniformare i nomi poiché quelli restituiti dalle richieste al DBMS arrivano tutti maiuscoli
                        // e lato client potrebbero non matchare con i nomi delle colonne presenti nel config.
                        row.DATA_FIELD_NAME = row.DATA_FIELD_NAME.ToUpper();
                    }

                    GridConfigManager.mColumnSettings[fieldsConfigName] = ds;
                }

                return ds;
            }
        }

        /// <summary>
        /// Pulisce la cache delle configurazioni. Se configNames è null pulisce tutto altrimenti solo le config passate
        /// </summary>
        /// <param name="configNames"></param>
        public void ClearCache(string[] configNames = null)
        {
            lock (GridConfigManager.mGridConfigs)
            {
                if (configNames == null)
                {
                    GridConfigManager.mGridConfigs.Clear();
                }
                else
                {
                    foreach (string config in configNames)
                    {
                        GridConfigManager.mGridConfigs.Remove(config);
                    }
                }
            }
        }

        public void ClearAllCache()
        {
            this.ClearCache();
            lock (GridConfigManager.mColumnSettings)
            {
                mColumnSettings.Clear();
            }
            lock(GridConfigManager.mFieldsConfig)
            {
                mFieldsConfig.Clear();
            }
            lock (GridConfigManager.mGridConfigs)
            {
                mGridConfigs.Clear();
            }
        }


        #region Creazione da DB

        /// <summary>
        ///  Crea il ColumnSettingsDS a partire dai dati ricavati dal DB
        /// </summary>
        /// <param name="dataStore"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static ColumnSettingsDS CreateColumnSettingsDSFromDB(IDataStore dataStore, string tableName)
        {
            ColumnSettingsDS ds = new ColumnSettingsDS();
           
            string[] disabledColsArr = null;
            string[] nonVisibleColsArr = null;
            string[] nonEditableColsArr = null;

            string nonVisibleColumns = ConfigurationManager.AppSettings["NON_VISIBLE_COLUMNS"];
            if (nonVisibleColumns != null)
            {
                nonVisibleColsArr = nonVisibleColumns.Split(';');
            }
            string disabledColumns = ConfigurationManager.AppSettings["DISABLED_COLUMNS"];
            if (disabledColumns != null)
            {
                disabledColsArr = disabledColumns.Split(';');
            }

            string nonEditableColumns = ConfigurationManager.AppSettings["NON_EDITABLE_COLUMNS"];
            if (nonEditableColumns != null)
            {
                nonEditableColsArr = nonEditableColumns.Split(';');
            }

            if (tableName.ToUpper().EndsWith("_EDIT"))
            {
                //Poi dobbiamo trovare un modo per gestire le configurazioni per l'editing
                tableName = tableName.Substring(0, tableName.Length - 5);
            }

            DataTable tbl = dataStore.GetTableMetadata(tableName);

            if (tbl != null)
            {
                bool hasFlagsField = false;
                int pos = 0;

                string primaryKey = "";
                bool hasMultiplePK = false;

                string geometryField = "";


                foreach (DataRow metadataRow in tbl.Select("", "FIELD_NAME"))
                {
                    if (metadataRow["FIELD_NAME"].ToString().ToUpper() == "GDB_GEOMATTR_DATA")
                    {
                        continue;
                    }
                    ColumnSettingsDS.COLUMN_SETTINGRow row = CreateSettingRow(ds, disabledColsArr, nonVisibleColsArr, nonEditableColsArr, pos, metadataRow);

                    if (row.DATA_FIELD_NAME.ToUpper() == "FLAGS")
                    {
                        hasFlagsField = true;
                    }

                    pos++;

                    ds.COLUMN_SETTING.AddCOLUMN_SETTINGRow(row);

                    if (row.TYPE == "IGeometry")
                    {
                        geometryField = row.DATA_FIELD_NAME;
                    }

                    if (!metadataRow.IsNull("PK_COLUMN_NAME"))
                    {
                        if (string.IsNullOrEmpty(primaryKey))
                        {
                            primaryKey = metadataRow["PK_COLUMN_NAME"].ToString();
                        }
                        else
                        {
                            primaryKey += "," + metadataRow["PK_COLUMN_NAME"].ToString();
                            hasMultiplePK = true;
                        }
                    }
                }

                if (!hasFlagsField)
                {
                    // Aggiunge il campo FLAGS

                    ColumnSettingsDS.COLUMN_SETTINGRow flagsRow = CreateFlagsRow(ds, disabledColsArr, nonVisibleColsArr, nonEditableColsArr, pos);

                    ds.COLUMN_SETTING.AddCOLUMN_SETTINGRow(flagsRow);
                }

                AddPKAndGeometryField(ds, primaryKey, hasMultiplePK, geometryField);

            }

           
            return ds;
        }

        /// <summary>
        /// Crea una riga di COLUMN_SETTING
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="disabledColsArr"></param>
        /// <param name="nonVisibleColsArr"></param>
        /// <param name="pos"></param>
        /// <param name="metadataRow"></param>
        /// <returns></returns>
        private static ColumnSettingsDS.COLUMN_SETTINGRow CreateSettingRow(ColumnSettingsDS ds, string[] disabledColsArr, string[] nonVisibleColsArr, string[] nonEditableColumns, int pos, DataRow metadataRow)
        {
            ColumnSettingsDS.COLUMN_SETTINGRow row = ds.COLUMN_SETTING.NewCOLUMN_SETTINGRow();

            row.DATA_FIELD_NAME = metadataRow["FIELD_NAME"].ToString();

            row.CAPTION = metadataRow.IsNull("ALIAS_NAME") ? metadataRow["FIELD_NAME"].ToString() : metadataRow["ALIAS_NAME"].ToString();
            bool colDisabled = false;
            if (disabledColsArr != null)
            {
                colDisabled = disabledColsArr.Contains(row.DATA_FIELD_NAME);
            }
            row.DISABLED = colDisabled;
            if (!metadataRow.IsNull("DOMAIN_NAME"))
            {
                row.DOMAIN = metadataRow["DOMAIN_NAME"].ToString();
            }
            row.EDITABLE = true;
            row.FILTERABLE = true;
            if (!metadataRow.IsNull("CHAR_LENGTH"))
            {
                row.LENGTH = Convert.ToInt32(metadataRow["CHAR_LENGTH"]);
            }
            row.NULLABLE = metadataRow["IS_NULLABLE"].ToString().ToUpper() == "YES";
            row.POSITION = pos;
            row.SHOW_IN_INFO = true;
            row.SHOW_IN_SUMMARY = true;
            if (!metadataRow.IsNull("SUB_TYPE_NAME"))
            {
                row.SUB_TYPE = metadataRow["SUB_TYPE_NAME"].ToString();
            }
            if (!metadataRow.IsNull("DATA_TYPE"))
            {
                string fieldType =  metadataRow["DATA_TYPE"].ToString();
                row.TYPE = GetFieldType(fieldType);
            }
            row.USE_AUTOCOMPLETE = false;

            row.IS_LOOKUP = !row.IsDOMAINNull() || !row.IsSUB_TYPENull(); //Mettere a true se c'è DOMAIN o SUB_TYPE

            bool colVisible = true;
            if (nonVisibleColsArr != null)
            {
                colVisible = !nonVisibleColsArr.Contains(row.DATA_FIELD_NAME);
            }
            row.VISIBLE = colVisible;

            return row;
        }

        /// <summary>
        /// Crea una riga per il campo FLAGS
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="disabledColsArr"></param>
        /// <param name="nonVisibleColsArr"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        private static ColumnSettingsDS.COLUMN_SETTINGRow CreateFlagsRow(ColumnSettingsDS ds, string[] disabledColsArr, string[] nonVisibleColsArr, string[] nonEditableColumns,  int pos)
        {
            ColumnSettingsDS.COLUMN_SETTINGRow flagsRow = ds.COLUMN_SETTING.NewCOLUMN_SETTINGRow();

            flagsRow.DATA_FIELD_NAME = "FLAGS";
            flagsRow.CAPTION = "FLAGS";
            bool colDisabled = false;
            if (disabledColsArr != null)
            {
                colDisabled = disabledColsArr.Contains("FLAGS");
            }
            flagsRow.DISABLED = colDisabled;
            flagsRow.EDITABLE = false;
            flagsRow.FILTERABLE = false;
            flagsRow.IS_LOOKUP = false;
            flagsRow.NULLABLE = true;
            flagsRow.POSITION = pos;
            flagsRow.SHOW_IN_INFO = false;
            flagsRow.SHOW_IN_SUMMARY = false;
            flagsRow.TYPE = "Int32";
            flagsRow.USE_AUTOCOMPLETE = false;

            SetRowPropertiesFromSetting(flagsRow, disabledColsArr, nonVisibleColsArr, nonEditableColumns);
                       
            return flagsRow;
        }

        private static void SetRowPropertiesFromSetting(ColumnSettingsDS.COLUMN_SETTINGRow settingRow, string[] disabledColsArr, string[] nonVisibleColsArr, string[] nonEditableColumns)
        {
            bool colDisabled = false;
            if (disabledColsArr != null)
            {
                colDisabled = disabledColsArr.Contains("FLAGS");
            }
            settingRow.DISABLED = colDisabled;

            bool colVisible = true;
            if (nonVisibleColsArr != null)
            {
                colVisible = !nonVisibleColsArr.Contains("FLAGS");
            }

            settingRow.VISIBLE = colVisible;

            bool colEditable = true;
            if (nonEditableColumns != null)
            {
                colEditable = !nonEditableColumns.Contains("FLAGS");
            }
            settingRow.EDITABLE = colEditable;
        }

        /// <summary>
        /// Aggiunge la chiave primaria e il campo geometria
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="primaryKey"></param>
        /// <param name="hasMultiplePK"></param>
        /// <param name="geometryField"></param>
        private static void AddPKAndGeometryField(ColumnSettingsDS ds, string primaryKey, bool hasMultiplePK, string geometryField)
        {
            ColumnSettingsDS.GRID_SETTINGRow settingRow = null;

            if (!string.IsNullOrEmpty(primaryKey) && !hasMultiplePK)
            {
                // Aggiunge PK_FIELD nella sezione GRID_SETTINGS.
                settingRow = ds.GRID_SETTING.NewGRID_SETTINGRow();
                settingRow.PK_FIELD = primaryKey;
            }

            if (!string.IsNullOrEmpty(geometryField))
            {
                if (settingRow == null)
                {
                    settingRow = ds.GRID_SETTING.NewGRID_SETTINGRow();
                }

                settingRow.GEOMETRY_FIELD = geometryField;
            }

            if (settingRow != null)
            {
                ds.GRID_SETTING.AddGRID_SETTINGRow(settingRow);
            }
        }

        /// <summary>
        /// Definisce il tipo della colonna a partire dall'informazione dal DB
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static string GetFieldType(string typeName)
        {

            if (typeName.ToUpper() == "GEOMETRY")
            {
                return "IGeometry";
            }

            Type dataType = Type.GetType(typeName);
            if (dataType == null)
            {
                throw new Exception($"Impossibile determinare il tipo: {typeName}");
            }

            switch (dataType.FullName)
            {
                case "System.Boolean":
                    return "bool";
                case "System.String":
                    return "string";
                case "System.Guid":
                    return "Guid";
                case "System.DateTime":
                    return "DateTime";
                case "System.Int16":
                    return "Int16";
                case "System.Int32":
                    return "Int32";
                case "System.Int64":
                    return "Int64";
                case "System.Decimal":
                case "System.Double":
                case "System.Single":
                    return "decimal";
                case "Microsoft.SqlServer.Types.SqlGeometry":
                    return "IGeometry";
                default:
                    throw new ArgumentOutOfRangeException("type", $"Unable to map this type {dataType.FullName}.");

            }

        }

        #endregion Creazione da DB

        private Interface.GridConfig BuildGridConfig(string fieldConfigName)
        {
            Interface.GridConfig gridConfig = new Interface.GridConfig();

            gridConfig.ConfigName = fieldConfigName;



            string idField = mEntityManager.DataEntityManager.IDField;
            FlagsOperationInfoDTO fOpInfo = null;
            string dataProviderName = null;

            MapConfigDS.MAP_SERVICE_LAYERRow[] foundRows = mMapConfigManager == null 
                ? null 
                : (MapConfigDS.MAP_SERVICE_LAYERRow[])mMapConfigManager.GetMapConfig().MAP_SERVICE_LAYER.Select("ATTRIBUTES_TABLE = '" + fieldConfigName + "' OR FIELDS_CONFIG_NAME = '" + fieldConfigName + "'");

            if (foundRows != null && foundRows.Length > 0)
            {
                idField = foundRows[0].KEY_FIELD;

                if (!foundRows[0].IsDATA_PROVIDER_NAMENull())
                {
                    dataProviderName = foundRows[0].DATA_PROVIDER_NAME;
                }

                fOpInfo = Utils.GetFlagsOperationInfoDTO(foundRows[0]);

                //Per ora facciamo così perché non siamo sicuri di prendere il layer giusto.
                //Bisognerebbe interrogare la configurazione di mappa a partire dal nome del layer e non dalla tabella degli atributi
                //In realtà questa logica non dovrebbe stare qui nella griglia, ma le informazioni provenienti dalla confiugrazione di mappa gli dovrebbero essere passati da fuori
                fOpInfo.TableFilter = null;
            }
                        



            ColumnSettingsDS ds = GetColSettings(fieldConfigName, dataProviderName);

            ColumnSettingsDS.GRID_SETTINGRow gridSettingsRow = null;

            if (ds.GRID_SETTING.Rows.Count > 0)
            {
                gridSettingsRow = ds.GRID_SETTING[0];
            }


            if (fOpInfo == null && fieldConfigName.EndsWith("_EDIT") && mMapConfigManager != null)
            {
                //Se stiamo richiedendo la configurazione per l'editing, 
                //proviamo ad usare la configurazione di base per costruire il FlagsOperationInfo
                //Serve per passarlo al GetRowsByCoordinates per gestire gli elementi sovrapposti
                string baseFieldConfigName = fieldConfigName.Substring(0, fieldConfigName.Length - 5);

                MapConfigDS.MAP_SERVICE_LAYERRow[] fRows = (MapConfigDS.MAP_SERVICE_LAYERRow[])mMapConfigManager.GetMapConfig().MAP_SERVICE_LAYER.Select("ATTRIBUTES_TABLE = '" + baseFieldConfigName + "' OR FIELDS_CONFIG_NAME = '" + baseFieldConfigName + "'");

                if (fRows != null && fRows.Length > 0)
                {
                    fOpInfo = Utils.GetFlagsOperationInfoDTO(fRows[0]);
                    //Per ora facciamo così perché non siamo sicuri di prendere il layer giusto.
                    //Bisognerebbe interrogare la configurazione di mappa a partire dal nome del layer e non dalla tabella degli atributi
                    //In realtà questa logica non dovrebbe stare qui nella griglia, ma le informazioni provenienti dalla confiugrazione di mappa gli dovrebbero essere passati da fuori
                    fOpInfo.TableFilter = null;
                }
            }

            if (fOpInfo == null)
            {
                fOpInfo = new FlagsOperationInfoDTO();

                fOpInfo.ConfigName = fieldConfigName;
                fOpInfo.EntityName = fieldConfigName;
                fOpInfo.GeometryType = "TABLE";
                fOpInfo.IDField = (gridSettingsRow != null && !gridSettingsRow.IsPK_FIELDNull()) ? gridSettingsRow.PK_FIELD : idField;
                fOpInfo.Srid = 0;
                fOpInfo.TipoOggetto = fieldConfigName;
            }

            gridConfig.FOpInfo = fOpInfo;

            gridConfig.UseCache = true;

            if (gridSettingsRow != null)
            {
                if (!gridSettingsRow.IsPK_FIELDNull())
                {
                    idField = gridSettingsRow.PK_FIELD;
                }

                gridConfig.UseCache = gridSettingsRow.IsUSE_CACHENull() ? true : gridSettingsRow.USE_CACHE;

                gridConfig.Alias = gridSettingsRow.IsALIASNull() ? fieldConfigName : gridSettingsRow.ALIAS;

                gridConfig.EnableAttachments = gridSettingsRow.IsENABLE_ATTACHMENTSNull() ? false : gridSettingsRow.ENABLE_ATTACHMENTS;


                if (!gridSettingsRow.IsUSE_SUBGRIDNull())
                {
                    gridConfig.UseSubgrid = gridSettingsRow.USE_SUBGRID;
                }

                if (!gridSettingsRow.IsSUBGRID_CONFIGNull())
                {
                    gridConfig.SubgridConfig = gridSettingsRow.SUBGRID_CONFIG;
                }

                if (!gridSettingsRow.IsSUBGRID_PARENT_FIELDNull())
                {
                    gridConfig.SubgridParentField = gridSettingsRow.SUBGRID_PARENT_FIELD;
                }

                if (!gridSettingsRow.IsSUBGRID_CHILD_FIELDNull())
                {
                    gridConfig.SubgridChildField = gridSettingsRow.SUBGRID_CHILD_FIELD;
                }

                if (!gridSettingsRow.IsHAS_CHILDREN_FIELDNull())
                {
                    gridConfig.HasChildrenField = gridSettingsRow.HAS_CHILDREN_FIELD;
                }

                if (!gridSettingsRow.IsREFRESH_RATENull())
                {
                    gridConfig.RefreshRate = gridSettingsRow.REFRESH_RATE;
                }
            }

            gridConfig.PKField = idField;
            

            string geometryField = mEntityManager.DataEntityManager.GeometryField;

            if (ds.COLUMN_SETTING.FindByDATA_FIELD_NAME(geometryField) != null)
            {
                gridConfig.GeometryField = geometryField;
            }

            if (gridSettingsRow != null)
            {
                if (!gridSettingsRow.IsGEOMETRY_FIELDNull())
                {
                    gridConfig.GeometryField = gridSettingsRow.GEOMETRY_FIELD;
                }
            }            

            if (gridConfig.GeometryField != null)
            {
                gridConfig.GeometryField = gridConfig.GeometryField.ToUpper();
            }

            gridConfig.ColumnsConfig = BuildFieldsConfig(fieldConfigName, dataProviderName);

            gridConfig.RelationsConfig = BuildRelationsConfig(fieldConfigName, dataProviderName);

            gridConfig.ClassificationConfigs = BuildClassificationConfigs(fieldConfigName, dataProviderName);

            // cerco le colonne che hanno dipendenze

            //gridConfig.DependencyDictionary = BuildDependencyInfo(gridConfig.ColumnsConfig);



            return gridConfig;
        }

        private List<ClassificationConfig> BuildClassificationConfigs(string fieldsConfigName, string dataProviderName)
        {
            ColumnSettingsDS ds = GetColSettings(fieldsConfigName, dataProviderName);

            Dictionary<string, ClassificationConfig> classDict = new Dictionary<string, ClassificationConfig>();

            foreach (ColumnSettingsDS.CLASSIFICATION_SETTINGSRow row in ds.CLASSIFICATION_SETTINGS)
            {
                if (!classDict.ContainsKey(row.ID))
                {
                    classDict.Add(row.ID, new ClassificationConfig() { ID = row.ID, TYPE = row.TYPE });
                }

                ClassificationConfig classConfig = classDict[row.ID];

                classConfig.ClassificationDefs.Add(new ClassDef() { Template = row.IsTEMPLATENull() ? "" : row.TEMPLATE, Description = row.IsDESCRIPTIONNull() ? "" : row.DESCRIPTION, Value = row.VALUE });
            }

            return classDict.Values.ToList();
        }


        //private Dictionary<string, Dictionary<string, List<object>>> BuildDependencyInfo(List<GridColumnConfig> colConfigs)
        //{
        //    Dictionary<string, Dictionary<string, List<object>>> depInfo = new Dictionary<string, Dictionary<string, List<object>>>();
            
        //    foreach(GridColumnConfig colConfig in colConfigs)
        //    {
        //        // nota: deve cercare anche per colonne che non sono lookup
        //        //if(colConfig.IS_LOOKUP)
        //        //{
        //            string key = colConfig.DATA_FIELD_NAME;

        //            if(colConfig.DEPENDENCY_FIELD != null && colConfig.DEPENDENCY_FIELD != "")
        //            {
        //                Dictionary<string, List<object>> di = GetDependencyInfo(colConfig);

        //                if(!depInfo.ContainsKey(key))
        //                {
        //                    depInfo.Add(key, new Dictionary<string, List<object>>());
        //                }

        //                if(di.Count > 0)
        //                {
        //                    depInfo[key] = di;
        //                }
        //            }
        //        //}
        //    }

        //    return depInfo;
        //}


        private List<GridColumnConfig> BuildFieldsConfig(string fieldsConfigName, string dataProviderName = null)
        {
            List<GridColumnConfig> fieldsConfig = new List<GridColumnConfig>();

            ColumnSettingsDS ds = GetColSettings(fieldsConfigName, dataProviderName);

            foreach (ColumnSettingsDS.COLUMN_SETTINGRow row in ds.COLUMN_SETTING.Select("", "POSITION"))
            {
                GridColumnConfig colConfig = new GridColumnConfig();

                colConfig.CAPTION = row.CAPTION;
                colConfig.DATA_FIELD_NAME = row.DATA_FIELD_NAME;
                colConfig.EDITABLE = row.EDITABLE;
                colConfig.FORMAT = row.IsFORMATNull() ? "" : row.FORMAT;
                colConfig.NULLABLE = row.NULLABLE;
                colConfig.TYPE = row.TYPE;

                if (row.IsLENGTHNull())
                {
                    string t = row.TYPE.ToLower();
                    if (t == "multilinestring" || t == "json" || t == "htmlcode")
                    {
                        colConfig.LENGTH = -1;
                    }
                    else
                    {
                        colConfig.LENGTH = 50;
                    }
                }
                else
                {
                    colConfig.LENGTH = row.LENGTH;
                }
                
                colConfig.VISIBLE = row.VISIBLE;
                colConfig.WIDTH = row.IsWIDTHNull() ? null : row.WIDTH;
                colConfig.IS_LOOKUP = row.IsIS_LOOKUPNull() ? false : row.IS_LOOKUP;
                colConfig.IS_DOMAIN = !row.IsDOMAINNull();
                colConfig.DOMAIN = row.IsDOMAINNull() ? null : row.DOMAIN;
                colConfig.SUB_TYPE = row.IsSUB_TYPENull() ? null : row.SUB_TYPE;
                colConfig.USE_AUTOCOMPLETE = row.IsUSE_AUTOCOMPLETENull() ? false : row.USE_AUTOCOMPLETE;

                colConfig.DEPENDENCY_FIELD = row.IsDEPENDENCY_FIELDNull() ? "" : row.DEPENDENCY_FIELD;
                colConfig.DEPENDENCY_TABLE = row.IsDEPENDENCY_TABLENull() ? "" : row.DEPENDENCY_TABLE;
                colConfig.DEPENDENCY_VALUE_FIELD = row.IsDEPENDENCY_VALUE_FIELDNull() ? "" : row.DEPENDENCY_VALUE_FIELD;
                colConfig.DEPENDENCY_MAP_VALUE_FIELD = row.IsDEPENDENCY_MAP_VALUE_FIELDNull() ? "" : row.DEPENDENCY_MAP_VALUE_FIELD;

                colConfig.POSITION = row.IsPOSITIONNull() ? 0 : row.POSITION;
                colConfig.SHOW_IN_SUMMARY = row.IsSHOW_IN_SUMMARYNull() ? false : row.SHOW_IN_SUMMARY;
                colConfig.SHOW_IN_INFO = row.IsSHOW_IN_INFONull() ? false : row.SHOW_IN_INFO;
                colConfig.DEFAULT_VALUE = row.IsDEFAULT_VALUENull() ? "" : row.DEFAULT_VALUE;
                colConfig.DEF_VALUE_GEOM_DEP_PARAMS = row.IsDEF_VALUE_GEOM_DEP_PARAMSNull() ? "" : row.DEF_VALUE_GEOM_DEP_PARAMS;

                colConfig.LOOKUP_MANAGER_NAME = row.IsLOOKUP_MANAGER_NAMENull() ? "" : row.LOOKUP_MANAGER_NAME;
                colConfig.LOOKUP_TABLE_NAME = row.IsLOOKUP_TABLE_NAMENull() ? "" : row.LOOKUP_TABLE_NAME;
                colConfig.LOOKUP_KEY_FIELD = row.IsLOOKUP_KEY_FIELDNull() ? "" : row.LOOKUP_KEY_FIELD;
                colConfig.LOOKUP_TEXT_FIELD = row.IsLOOKUP_TEXT_FIELDNull() ? "" : row.LOOKUP_TEXT_FIELD;
                colConfig.LOOKUP_FILTER = row.IsLOOKUP_FILTERNull() ? "" : row.LOOKUP_FILTER;
                colConfig.FILTERABLE = row.IsFILTERABLENull() ? true : row.FILTERABLE;

                colConfig.FK_FIELD = row.IsFK_FIELDNull() ? "" : row.FK_FIELD;
                colConfig.REL_FK_FIELD = row.IsREL_FK_FIELDNull() ? "" : row.REL_FK_FIELD;
                colConfig.REL_TABLE_NAME = row.IsREL_TABLE_NAMENull() ? "" : row.REL_TABLE_NAME;
                colConfig.REL_TABLE_FILTER = row.IsREL_TABLE_FILTERNull() ? "" : row.REL_TABLE_FILTER;

                colConfig.HOT_LINK_TEXT = row.IsHOT_LINK_TEXTNull() ? "" : row.HOT_LINK_TEXT;
                colConfig.HOT_LINK_TYPE = row.IsHOT_LINK_TYPENull() ? "" : row.HOT_LINK_TYPE;
                colConfig.HOT_LINK_PARAMS = row.IsHOT_LINK_PARAMSNull() ? "" : row.HOT_LINK_PARAMS;
                colConfig.HOT_LINK_GROUP = row.IsHOT_LINK_GROUPNull() ? "" : row.HOT_LINK_GROUP;
                colConfig.HOT_LINK_CONDITION_FIELD = row.IsHOT_LINK_CONDITION_FIELDNull() ? "" : row.HOT_LINK_CONDITION_FIELD;
                colConfig.HOT_LINK_CONDITION_EXPRESSION = row.IsHOT_LINK_CONDITION_EXPRESSIONNull() ? "" : row.HOT_LINK_CONDITION_EXPRESSION;

                colConfig.DISABLED = row.IsDISABLEDNull() ? false : row.DISABLED;

                colConfig.CLASSIFICATION_ID = row.IsCLASSIFICATION_IDNull() ? "" : row.CLASSIFICATION_ID;
                colConfig.CLASSIFICATION_FIELD = row.IsCLASSIFICATION_FIELDNull() ? "" : row.CLASSIFICATION_FIELD;


                colConfig.HELP_MSG = row.IsHELP_MSGNull() ? "" : row.HELP_MSG;
                colConfig.URL = row.IsURLNull() ? "" : row.URL;

                colConfig.CATEGORY = row.IsCATEGORYNull() ? "" : row.CATEGORY;
                colConfig.GROUP = row.IsGROUPNull() ? "" : row.GROUP;

                colConfig.IS_UNIQUE = row.IsIS_UNIQUENull() ? false : row.IS_UNIQUE;
                colConfig.JSONPART_JSON_FIELD = row.IsJSONPART_JSON_FIELDNull() ? "" : row.JSONPART_JSON_FIELD;

                // se è una lookup e non uso l'autocomplete allora genero tutti i valori di lookup (verranno visualizzati in una combobox).
                // le lookup con filtri spaziali le salto, visto che sono gestite con un metodo a parte.
                if (!row.IsIS_LOOKUPNull() && row.IS_LOOKUP && (row.IsUSE_AUTOCOMPLETENull() || !row.USE_AUTOCOMPLETE)
                    && (row.IsLOOKUP_FILTERNull() || !row.LOOKUP_FILTER.StartsWith("GEOM_")))
                {
                    ColumnSettingsDS.COLUMN_SETTINGRow[] depChildren = GetDepChildren(ds, row.DATA_FIELD_NAME); //  (ColumnSettingsDS.COLUMN_SETTINGRow[])ds.COLUMN_SETTING.Select("DEPENDENCY_FIELD = '" + row.DATA_FIELD_NAME + "'");

                    if (depChildren.Length > 0)
                    {
                        //Dictionary<object, Dictionary<string, List<object>>> depInfo = GetDependencyInfo(colConfig, depChildren);
                        //colConfig.DependencyDictionary = depInfo;

                        Dictionary<string, List<string>> depDict = GetDependencyFieldDict(depChildren);
                        colConfig.DependencyFieldsDict = depDict;
                    }

                    colConfig.LookupInfo = GetLookupInfo(row, null, dataProviderName);
                }

                if (!row.IsDEPENDENCY_FIELDNull())
                {
                    string[] keyFields = colConfig.DEPENDENCY_FIELD.Split(';');

                    if (keyFields.All((f) =>
                    {
                        ColumnSettingsDS.COLUMN_SETTINGRow[] parentRows = ds.COLUMN_SETTING.AsEnumerable().Where((r) => r.DATA_FIELD_NAME.ToUpper() == f.ToUpper()).ToArray();

                        if (parentRows.Length > 0)
                        {
                            ColumnSettingsDS.COLUMN_SETTINGRow parentRow = parentRows[0];

                            return (!parentRow.IsIS_LOOKUPNull() && parentRow.IS_LOOKUP && (row.IsUSE_AUTOCOMPLETENull() || !parentRow.USE_AUTOCOMPLETE)
                                    && (parentRow.IsLOOKUP_FILTERNull() || !parentRow.LOOKUP_FILTER.StartsWith("GEOM_")));
                        }

                        return false;
                    }))
                    {
                        colConfig.DependencyDictionary = GetDependencyInfo(colConfig);
                    }
                }

                fieldsConfig.Add(colConfig);
            }

            //// costruisco una volta per tutte le dipendenze da lookup che non hanno filtro spaziale

            /// Questo non serve più, lo abbiamo già fatto in GetLookupInfo

            //List<GridColumnConfig> lstColConfigs = BuildDependencyInfo(fieldsConfig, fieldsConfigName, false);

            //foreach (GridColumnConfig colConfig in lstColConfigs)
            //{
            //    GridColumnConfig foundColConfig = fieldsConfig.Find(c => c.DATA_FIELD_NAME == colConfig.DATA_FIELD_NAME);
            //    if (foundColConfig != null)
            //    {
            //        foundColConfig.DependencyDictionary = colConfig.DependencyDictionary;
            //        foundColConfig.LookupInfo = colConfig.LookupInfo;
            //    }
            //}

            return fieldsConfig;
        }        

        private List<RelationConfig> BuildRelationsConfig(string fieldsConfigName, string dataProviderName)
        {
            List<RelationConfig> relationsConfig = new List<RelationConfig>();

            ColumnSettingsDS ds = GetColSettings(fieldsConfigName, dataProviderName);

            foreach (ColumnSettingsDS.RELATION_SETTINGRow row in ds.RELATION_SETTING)
            {
                RelationConfig relConfig = new RelationConfig();

                relConfig.PARENT_KEY_FIELD = row.PARENT_KEY_FIELD;
                relConfig.REL_CHILD_TABLE_NAME = row.REL_CHILD_TABLE_NAME;
                relConfig.REL_CHILD_FK_FIELD = row.REL_CHILD_FK_FIELD;
                relConfig.REL_CAPTION = row.REL_CAPTION;
                relConfig.LINK_FIELD = row.IsLINK_FIELDNull() ? null : row.LINK_FIELD;

                relationsConfig.Add(relConfig);
            }

            return relationsConfig;
        }

        /// <summary>
        /// Resituisce il valore di default per una colonna dipendente dalla geometria dell'oggetto
        /// </summary>
        /// <param name="dvParams"></param>
        /// <param name="geometry"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetGeomDepDefaultValues(Dictionary<string, DefValueGeomDepParams> dvParams, Geometry geometry)
        {
            Dictionary<string, string> defValues = new Dictionary<string, string>();

            IDatasetEntityManager entityManager = GetDatasetEntityManager(dvParams.First().Value.DATA_PROVIDER_NAME);

            List<SpatialFilter> spFilters = new List<SpatialFilter>();

            SpatialFilter spFilter = new SpatialFilter(geometry, dvParams.First().Value.DISTANCE, true);

            spFilters.Add(spFilter);

            PageDefinition pgDef = new PageDefinition();

            pgDef.PageIndex = 1;
            pgDef.PageSize = 1;
            pgDef.SortField = dvParams.First().Value.SRC_FIELD;

            string geomField = (string.IsNullOrEmpty(dvParams.First().Value.SRC_GEOM_FIELD)) ? mEntityManager.DataEntityManager.DataStore.GeometryField : dvParams.First().Value.SRC_GEOM_FIELD;

            List<string> selectFields = new List<string>(dvParams.Values.Select((dvp) => { return dvp.SRC_FIELD; }));

            selectFields.Add(geomField);

            PageResult res = entityManager.GetTablePaged(dvParams.First().Value.SRC_TABLE, selectFields: selectFields.ToArray(), pageDef: pgDef, spatialFilters: spFilters, returnsTotRowsCount: false);

            if (res != null && res.TablePage != null && res.TablePage.Rows.Count > 0)
            {
                foreach (string fieldName in dvParams.Keys)
                {
                    DefValueGeomDepParams dvp = dvParams[fieldName];

                    if (!res.TablePage.Rows[0].IsNull(dvp.SRC_FIELD))
                    {
                        defValues.Add(fieldName, res.TablePage.Rows[0][dvp.SRC_FIELD].ToString());
                    }
                }
            }

            return defValues;
        }

        private List<LookupItem> GetLookupInfo(ColumnSettingsDS.COLUMN_SETTINGRow row, Geometry geometry = null, string dataProviderName = null, object editingId = null)
        {
            List<LookupItem> lkItems = new List<LookupItem>();

            Filter filter = null;
            Filter editingRowFilter = null;
            List<SpatialFilter> spFilters = null;

            string lookupProviderName = (row.IsLOOKUP_MANAGER_NAMENull()) ? dataProviderName : row.LOOKUP_MANAGER_NAME;
            IDatasetEntityManager lookupEntityManager = GetDatasetEntityManager(lookupProviderName);

            if (!row.IsLOOKUP_FILTERNull())
            {
                ColumnSettingsDS colDs = GetColSettings(row.LOOKUP_TABLE_NAME, lookupProviderName);

                if (row.LOOKUP_FILTER.StartsWith("GEOM_"))
                {
                    string[] filterComponents = row.LOOKUP_FILTER.Split('=');

                    double distance = double.Parse(filterComponents[1], System.Globalization.CultureInfo.InvariantCulture);

                    SpatialFilter spFilter = new SpatialFilter(geometry, distance, true);

                    spFilters = new List<SpatialFilter>();
                    spFilters.Add(spFilter);

                }
                else
                {
                    filter = Infrastructure.DataUtils.Utils.ParseFilter(row.LOOKUP_FILTER, colDs);
                }

                if (editingId != null && row.LOOKUP_KEY_FIELD != null)
                {
                    string editingFilter = editingId.GetType() == typeof(string) ?
                            $@"{row.LOOKUP_KEY_FIELD} = '{editingId}'"
                            : $@"{row.LOOKUP_KEY_FIELD} = '{editingId.ToString().Replace("'", "''")}'";

                    editingRowFilter = Infrastructure.DataUtils.Utils.ParseFilter(editingFilter, colDs);
                }

            }            

            if (!row.IsDOMAINNull() && row.DOMAIN != "")
            {
                IDomainManager domManager = (string.IsNullOrEmpty(dataProviderName)) ? mDomainManager : DataFactory.Factory.GetDomainManager(lookupEntityManager.DataStore);

                IDomain domain = domManager.GetDomain(row.DOMAIN);

                if (domain != null)
                {
                    foreach (KeyValuePair<string, string> kp in domain.DomainValues)
                    {
                        LookupItem lkItem = new LookupItem();
                        lkItem.Value = kp.Key;
                        lkItem.Description = kp.Value;

                        lkItems.Add(lkItem);
                    }
                }
            }
            else if (row.DATA_FIELD_NAME.StartsWith("SUB_") || !row.IsSUB_TYPENull())
            {
                // questo lo mantengo per retrocompatibilità
                string fcName = "";

                if (!row.IsLOOKUP_FILTERNull())
                {
                    fcName = row.LOOKUP_FILTER.Split('=')[1].Replace("'", "").Trim();
                }

                if (!row.IsSUB_TYPENull() && !string.IsNullOrEmpty(row.SUB_TYPE))
                {
                    fcName = row.SUB_TYPE;
                }

                ISubTypeManager subTypeManager = (string.IsNullOrEmpty(dataProviderName)) ? mSubTypeManager : DataFactory.Factory.GetSubTypeManager(lookupEntityManager.DataStore);

                ISubType subType = subTypeManager.GetSubType(fcName);

                if (subType != null)
                {
                    foreach (KeyValuePair<string, string> kp in subType.SubTypeValues)
                    {
                        LookupItem lkItem = new LookupItem();
                        lkItem.Value = kp.Key;
                        lkItem.Description = kp.Value;

                        lkItems.Add(lkItem);
                    }
                }
            }
            else
            {
                DataTable tb = lookupEntityManager.GetTable(row.LOOKUP_TABLE_NAME, new string[] { row.LOOKUP_KEY_FIELD, row.LOOKUP_TEXT_FIELD }, filter: filter, spatialFilters: spFilters);

                HashSet<string> distinctHS = new HashSet<string>();

                foreach (DataRow dr in tb.Rows)
                {
                    if (!distinctHS.Contains(dr[row.LOOKUP_KEY_FIELD].ToString()))
                    {
                        distinctHS.Add(dr[row.LOOKUP_KEY_FIELD].ToString());

                        LookupItem lkItem = new LookupItem();

                        lkItem.Value = dr[row.LOOKUP_KEY_FIELD];
                        lkItem.Description = dr[row.LOOKUP_TEXT_FIELD].ToString();

                        lkItems.Add(lkItem);
                    }
                }

                if (editingRowFilter != null)
                {
                    DataTable tb2 = lookupEntityManager.GetTable(row.LOOKUP_TABLE_NAME, new string[] { row.LOOKUP_KEY_FIELD, row.LOOKUP_TEXT_FIELD }, filter: editingRowFilter, spatialFilters: null);

                    foreach (DataRow dr in tb2.Rows)
                    {
                        if (!distinctHS.Contains(dr[row.LOOKUP_KEY_FIELD].ToString()))
                        {
                            distinctHS.Add(dr[row.LOOKUP_KEY_FIELD].ToString());

                            LookupItem lkItem = new LookupItem();

                            lkItem.Value = dr[row.LOOKUP_KEY_FIELD];
                            lkItem.Description = dr[row.LOOKUP_TEXT_FIELD].ToString();

                            lkItems.Add(lkItem);
                        }
                    }
                }
            }

            return lkItems;
        }




        /// <summary>
        /// Restituisce la dependencyInfo di una colonna che dipende da un altra colonna
        /// </summary>
        /// <param name="colConfig"></param>
        /// <param name="parentValuesDict">Dizionario con le liste dei valori del padre su cui filtrare</param>
        /// <returns></returns>
        private Dictionary<string, List<object>> GetDependencyInfo(GridColumnConfig colConfig, Dictionary<string, Tuple<DataTypeEnum, object[]>> parentValuesDict = null)
        {
            // List<LookupItem> lookupInfo = new List<LookupItem>();
            Dictionary<string, List<object>> dependencyInfo = new Dictionary<string, List<object>>();

            List<string> selectFields = new List<string>();

            // string parents = colConfig.DEPENDENCY_FIELD;
            string table = colConfig.DEPENDENCY_TABLE;
            string[] keyFields = colConfig.DEPENDENCY_VALUE_FIELD.Split(';');
            string valueField = colConfig.DEPENDENCY_MAP_VALUE_FIELD;

            bool isLookup = colConfig.IS_LOOKUP;

            foreach(string key in keyFields)
            {
                selectFields.Add(key);
            }
            selectFields.Add(valueField);
            string lookupManagerName = colConfig.LOOKUP_MANAGER_NAME != null ? colConfig.LOOKUP_MANAGER_NAME : null;

            Filter filter = null;

            if (parentValuesDict != null)
            {
                filter = new Filter();

                filter.Operator = GroupOperatorEnum.AND;
                filter.Rules = new List<DataAccess.Infrastructure.Rule>();

                string[] parentFields = colConfig.DEPENDENCY_FIELD.Split(';');

                for (int i = 0; i < parentFields.Length; i++)
                {
                    string parentField = parentFields[i].ToUpper();

                    if (parentValuesDict.ContainsKey(parentField)) 
                    {
                        DataAccess.Infrastructure.Rule rule = new DataAccess.Infrastructure.Rule();

                        rule.Field = keyFields[i];
                        rule.DataType = parentValuesDict[parentField].Item1;
                        rule.Operator = OperatorEnum.InSet;
                        rule.Value = parentValuesDict[parentField].Item2;

                        filter.Rules.Add(rule);
                    }
                }
            }

            DataTable tb = GetDatasetEntityManager(lookupManagerName).GetTable(tableName: table, selectFields: selectFields.ToArray(), filter: filter);

            foreach(DataRow dr in tb.Rows)
            {
                //Non c'è bisogno di controllare il null perché è chiave
                string parentKeyValue = keyFields.Select((kf) => dr[kf].ToString()).Aggregate((kf1, kf2) => kf1 + ";" + kf2);

                if(!dependencyInfo.ContainsKey(parentKeyValue))
                {
                    dependencyInfo[parentKeyValue] = new List<object>();
                }

                if(!dr.IsNull(valueField))
                {
                    if(isLookup || dependencyInfo[parentKeyValue].Count == 0)
                    {
                        if (!dependencyInfo[parentKeyValue].Contains(dr[valueField]))
                        {
                            dependencyInfo[parentKeyValue].Add(dr[valueField]);
                        }
                    }
                }
                
            }
            
            return dependencyInfo; 
        }



        ///// <summary>
        ///// Restituisce la lookupInfo e la dependencyInfo di una colonna che ha campi dipendente da essa
        ///// </summary>
        ///// <param name="srcColConfig"></param>
        ///// <param name="configName"></param>
        ///// <returns></returns>
        //private Dictionary<object, Dictionary<string, List<object>>> GetDependencyInfo(GridColumnConfig srcColConfig, ColumnSettingsDS.COLUMN_SETTINGRow[] depChildren, Geometry geometry = null)
        //{
        //    // List<LookupItem> lookupInfo = new List<LookupItem>();
        //    Dictionary<object, Dictionary<string, List<object>>> dependencyInfo = new Dictionary<object, Dictionary<string, List<object>>>();

        //    //selectFields.Add(srcColConfig.LOOKUP_KEY_FIELD);
        //    //selectFields.Add(srcColConfig.LOOKUP_TEXT_FIELD);

        //    foreach(ColumnSettingsDS.COLUMN_SETTINGRow depColSettingsRow in depChildren)
        //    {
        //        //selectFields.Add(depColSettingsRow.DATA_FIELD_NAME);

        //        List<string> selectFields = new List<string>();

        //        // string[] parents = depColSettingsRow.DEPENDENCY_FIELD.Split(';');
        //        string parents = depColSettingsRow.DEPENDENCY_FIELD;
        //        string table = depColSettingsRow.DEPENDENCY_TABLE;
        //        string[] keyFields = depColSettingsRow.DEPENDENCY_VALUE_FIELD.Split(';');
        //        string valueField = depColSettingsRow.DEPENDENCY_MAP_VALUE_FIELD;

        //        bool isLookup = !depColSettingsRow.IsIS_LOOKUPNull() && depColSettingsRow.IS_LOOKUP;

        //        //foreach(string parent in parents.Split(';'))
        //        //{
        //        //    selectFields.Add(parent);
        //        //}
        //        foreach(string key in keyFields)
        //        {
        //            selectFields.Add(key);
        //        }
        //        selectFields.Add(valueField);


        //        //Filter filter = null;
        //        //List<SpatialFilter> spFilters = null;

        //        //if(srcColConfig.LOOKUP_FILTER != null)
        //        //{
        //        //    if(srcColConfig.LOOKUP_FILTER.StartsWith("GEOM_"))
        //        //    {
        //        //        string[] filterComponents = srcColConfig.LOOKUP_FILTER.Split('=');

        //        //        double distance = double.Parse(filterComponents[1], System.Globalization.CultureInfo.InvariantCulture);

        //        //        SpatialFilter spFilter = new SpatialFilter(geometry, distance);

        //        //        spFilters = new List<SpatialFilter>();
        //        //        spFilters.Add(spFilter);
        //        //    }
        //        //    else
        //        //    {
        //        //        ColumnSettingsDS colDs = GetColSettings(srcColConfig.LOOKUP_TABLE_NAME, srcColConfig.LOOKUP_MANAGER_NAME);

        //        //        filter = Infrastructure.DataUtils.Utils.ParseFilter(srcColConfig.LOOKUP_FILTER, colDs);
        //        //    }
        //        //}

        //        string lookupManagerName = (depColSettingsRow.IsLOOKUP_MANAGER_NAMENull()) ? null : depColSettingsRow.LOOKUP_MANAGER_NAME;

        //        DataTable tb = GetDatasetEntityManager(lookupManagerName).GetTable(table, selectFields.ToArray());

        //        foreach(DataRow dr in tb.Rows)
        //        {
        //            //Non c'è bisogno di controllare il null perché è chiave
        //            string parentKeyValue = keyFields.Select((kf) => dr[kf].ToString()).Aggregate((kf1, kf2) => kf1 + ";" + kf2);                    

        //            if(!dependencyInfo.ContainsKey(parentKeyValue))
        //            {
        //                dependencyInfo[parentKeyValue] = new Dictionary<string, List<object>>();
        //            }

        //            if(!dependencyInfo[parentKeyValue].ContainsKey(depColSettingsRow.DATA_FIELD_NAME))
        //            {
        //                dependencyInfo[parentKeyValue].Add(depColSettingsRow.DATA_FIELD_NAME, new List<object>());
        //            }

        //            if(!dr.IsNull(valueField))
        //            {
        //                if(!dependencyInfo[parentKeyValue][depColSettingsRow.DATA_FIELD_NAME].Contains(dr[valueField]))
        //                {
        //                    // verifico che il child sia lookup: in quel caso aggiungo tutti i valori restituiti
        //                    // se non è lookup inserisco solo il primo
        //                    if(isLookup || dependencyInfo[parentKeyValue][depColSettingsRow.DATA_FIELD_NAME].Count == 0)
        //                    {
        //                        dependencyInfo[parentKeyValue][depColSettingsRow.DATA_FIELD_NAME].Add(dr[valueField]);
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    // bonifico lookup con valori null
        //    //col nuovo modo non dovrebbe servire
        //    foreach(KeyValuePair<object, Dictionary<string, List<object>>> entry in dependencyInfo)
        //    {
        //        foreach(KeyValuePair<string, List<object>> e in entry.Value)
        //        {
        //            if (e.Value.Count == 0)
        //            {
        //                e.Value.Add(null);
        //            }
        //        }
        //    }

        //    return dependencyInfo; //  new Tuple<List<LookupItem>, Dictionary<object, Dictionary<string, List<object>>>>(lookupInfo, dependencyInfo);
        //}


        ///// <summary>
        ///// Restituisce la lookupInfo e la dependencyInfo di una colonna che ha campi dipendente da essa
        ///// </summary>
        ///// <param name="srcColConfig"></param>
        ///// <param name="configName"></param>
        ///// <returns></returns>
        //private Tuple<List<LookupItem>, Dictionary<object, Dictionary<string, List<object>>>> GetDependencyInfo(bool old, GridColumnConfig srcColConfig, ColumnSettingsDS.COLUMN_SETTINGRow[] depChildren, Geometry geometry = null)
        //{
        //    List<LookupItem> lookupInfo = new List<LookupItem>();
        //    Dictionary<object, Dictionary<string, List<object>>> dependencyInfo = new Dictionary<object, Dictionary<string, List<object>>>();

        //    List<string> selectFields = new List<string>();

            
        //    selectFields.Add(srcColConfig.LOOKUP_KEY_FIELD);
        //    selectFields.Add(srcColConfig.LOOKUP_TEXT_FIELD);

        //    foreach (ColumnSettingsDS.COLUMN_SETTINGRow depColSettingsRow in depChildren)
        //    {
        //        selectFields.Add(depColSettingsRow.DATA_FIELD_NAME);
            
        //    }

        //    Filter filter = null;
        //    List<SpatialFilter> spFilters = null;

        //    if (srcColConfig.LOOKUP_FILTER != null)
        //    {
        //        if (srcColConfig.LOOKUP_FILTER.StartsWith("GEOM_"))
        //        {
        //            string[] filterComponents = srcColConfig.LOOKUP_FILTER.Split('=');

        //            double distance = double.Parse(filterComponents[1], System.Globalization.CultureInfo.InvariantCulture);

        //            SpatialFilter spFilter = new SpatialFilter(geometry, distance);

        //            spFilters = new List<SpatialFilter>();
        //            spFilters.Add(spFilter);
        //        }
        //        else
        //        {
        //            ColumnSettingsDS colDs = GetColSettings(srcColConfig.LOOKUP_TABLE_NAME, srcColConfig.LOOKUP_MANAGER_NAME);

        //            filter = Infrastructure.DataUtils.Utils.ParseFilter(srcColConfig.LOOKUP_FILTER, colDs);
        //        }
        //    }

        //    // usare la configurazione con i campi che aggiungiamo
        //    DataTable tb = GetDatasetEntityManager(srcColConfig.LOOKUP_MANAGER_NAME).GetTable(srcColConfig.LOOKUP_TABLE_NAME, selectFields.ToArray(), filter: filter, spatialFilters: spFilters);

        //    HashSet<string> distinctHS = new HashSet<string>();

        //    foreach (DataRow dr in tb.Rows)
        //    {
        //        string parentKeyValue = dr[srcColConfig.LOOKUP_KEY_FIELD].ToString();

        //        if (string.IsNullOrWhiteSpace(parentKeyValue))
        //        {
        //            continue;
        //        }

        //        if (!distinctHS.Contains(parentKeyValue))
        //        {
        //            distinctHS.Add(parentKeyValue);

        //            LookupItem lkItem = new LookupItem();

        //            lkItem.Value = dr[srcColConfig.LOOKUP_KEY_FIELD];
        //            lkItem.Description = dr[srcColConfig.LOOKUP_TEXT_FIELD].ToString();

        //            lookupInfo.Add(lkItem);
        //        }

        //        if (!dependencyInfo.ContainsKey(parentKeyValue))
        //        {
        //            dependencyInfo[parentKeyValue] = new Dictionary<string, List<object>>();
        //        }

        //        foreach (ColumnSettingsDS.COLUMN_SETTINGRow depColSettingsRow in depChildren)
        //        {
        //            if (!dependencyInfo[parentKeyValue].ContainsKey(depColSettingsRow.DATA_FIELD_NAME))
        //            {
        //                dependencyInfo[parentKeyValue].Add(depColSettingsRow.DATA_FIELD_NAME, new List<object>());
        //            }

        //            if (!dr.IsNull(depColSettingsRow.DATA_FIELD_NAME))
        //            {
        //                if (!dependencyInfo[parentKeyValue][depColSettingsRow.DATA_FIELD_NAME].Contains(dr[depColSettingsRow.DATA_FIELD_NAME]))
        //                {
        //                    dependencyInfo[parentKeyValue][depColSettingsRow.DATA_FIELD_NAME].Add(dr[depColSettingsRow.DATA_FIELD_NAME]);
        //                }
        //            }
        //        }
        //    }

        //    return new Tuple<List<LookupItem>, Dictionary<object, Dictionary<string, List<object>>>>(lookupInfo, dependencyInfo);
        //}

        private DataTypeEnum ParseRuleDataType(string type)
        {
            if (type == "Int64")
            {
                return DataTypeEnum.Int64;
            }
            else if (type == "Int32")
            {
                return DataTypeEnum.Int32;
            }
            else if (type == "double")
            {
                return DataTypeEnum.Double;
            }
            else if (type == "decimal")
            {
                return DataTypeEnum.Decimal;
            }
            else if (type == "DateTime")
            {
                return DataTypeEnum.DateTime;
            }
            else
            {
                return DataTypeEnum.Text;
            }
        }

        protected IDatasetEntityManager GetDatasetEntityManager(string dataProviderName)
        {

            if (!string.IsNullOrEmpty(dataProviderName))
            {
                return DataFactory.Factory.GetDatasetEntityManager(dataProviderName, mSessionManager);
            }
            else
            {
                return mEntityManager.DataEntityManager;

            }

        }


        private ColumnSettingsDS.COLUMN_SETTINGRow[] GetDepChildren(ColumnSettingsDS ds, string srcField)
        {
//            string cmd = $@"DEPENDENCY_FIELD = '{srcField}' 
//OR DEPENDENCY_FIELD LIKE '%;{srcField}' 
//OR DEPENDENCY_FIELD LIKE '{srcField};%' 
//OR DEPENDENCY_FIELD LIKE '%;{srcField};%'";   
            
//            return (ColumnSettingsDS.COLUMN_SETTINGRow[])ds.COLUMN_SETTING.Select(cmd);

            return ds.COLUMN_SETTING.AsEnumerable().Where((dr) => !dr.IsDEPENDENCY_FIELDNull() && dr.DEPENDENCY_FIELD.ToUpper().Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Contains(srcField.ToUpper())).ToArray();


        }
        


        private Dictionary <string, List<string>> GetDependencyFieldDict(ColumnSettingsDS.COLUMN_SETTINGRow[]  depChildren)
        {
            Dictionary<string, List<string>> dict = new Dictionary<string, List<string>>();
            foreach(ColumnSettingsDS.COLUMN_SETTINGRow depChild in depChildren)
            {
                if(!dict.ContainsKey(depChild.DATA_FIELD_NAME))
                {
                    dict.Add(depChild.DATA_FIELD_NAME, new List<string>());
                }
                foreach(string key in depChild.DEPENDENCY_FIELD.Split(';'))
                {
                    dict[depChild.DATA_FIELD_NAME].Add(key);
                }
            }
            return dict;
        }
    }

}
