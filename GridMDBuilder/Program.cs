using DataStore.Interface;
using GridConfig.Interface;
using System;
using System.Data;
using System.IO;

namespace GridMDBuilder
{
    class Program
    {
        private static string mXmlPath = "";
        private static string mAppCode = "";
        private static GridConfigSettingsDS mGridConfigSettingsDS = new GridConfigSettingsDS();

        static void Main(string[] args)
        {
            WelcomeMessage();

            mXmlPath = GetXmlPath();
            mAppCode = GetAppCode();

            GetFiles();

            SaveToDatabase();
        }

        private static void WelcomeMessage()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("**********************************************");
            Console.WriteLine("GridMDBuilder 1.0.0");
            Console.WriteLine("Permette di salvare le configurazioni delle griglie in database");
            Console.WriteLine("Premere un qualiasi tasto per continuare...");
            Console.ReadLine();
        }

        private static string GetXmlPath()
        {
            var path = "";
            Console.Clear();
            Console.ResetColor();
            Console.WriteLine("**********************************************");
            Console.WriteLine("Inserire il percorso degli files XML contenenti le configurazioni.");
            path = Console.ReadLine();

            try
            {
                if (Directory.Exists(path) && path != null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Percorso valido.");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Errore: " + ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
                GetXmlPath();
            }

            return path;
        }

        private static string GetAppCode()
        {
            var appCode = "";
            Console.Clear();
            Console.ResetColor();
            Console.WriteLine("**********************************************");
            Console.WriteLine("Inserire il codice applicativo.");
            appCode = Console.ReadLine();

            try
            {
                if (appCode != null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("CODICE APPLICATIVO VALIDO.");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Errore: " + ex.Message);
                Console.ResetColor();
                GetAppCode();
            }

            return appCode;
        }

        private static void GetFiles()
        {
            Console.Clear();
            var files = Directory.GetFiles(mXmlPath, "*.xml");
            if (files.Length > 0)
            {
                foreach (var file in files)
                {
                    var mFileName = Path.GetFileNameWithoutExtension(file);
                    mFileName = mFileName.Replace("Config", "");
                    PopulateColumnSettingsDs(file, mFileName);
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Nessun file trovato.");
                Console.ResetColor();
                Console.WriteLine("Ripetere l'operazione.");
                mXmlPath = GetXmlPath();
            }
        }

        private static void PopulateColumnSettingsDs(string file, string fileName)
        {
            try
            {
                Console.ResetColor();
                Console.WriteLine("\n*******************************");
                Console.WriteLine("Lettura in corso file: " + file);
                ColumnSettingsDS mColumnSettingsDS = new ColumnSettingsDS();
                mColumnSettingsDS.ReadXml(file);
                PopulateGridConfigSettingsDs(mColumnSettingsDS, fileName);
                Console.ResetColor();
                Console.WriteLine("*******************************");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Errore: " + ex.Message);
                Console.WriteLine("XML malformato: " + file);
            }
        }

        private static void PopulateGridConfigSettingsDs(ColumnSettingsDS mColumnSettingsDS, string fileName)
        {
            Console.ResetColor();
            Console.WriteLine("Transferimento dei dati...");

            DataTable mColumnSettingsTable = mColumnSettingsDS.COLUMN_SETTING;
            DataTable mRelationSettingsTable = mColumnSettingsDS.RELATION_SETTING;
            DataTable mGridSettingDataTable = mColumnSettingsDS.GRID_SETTING;
            DataTable mtClassificationSettings = mColumnSettingsDS.CLASSIFICATION_SETTINGS;

            DataTable mDColumnSettingsTable = mGridConfigSettingsDS.MD_GRID_COLUMN_SETTING;
            DataTable mDRelationSettingsTable = mGridConfigSettingsDS.MD_GRID_RELATION_SETTING;
            DataTable mDGridSettingDataTable = mGridConfigSettingsDS.MD_GRID_SETTING;
            DataTable mDtClassificationSettings = mGridConfigSettingsDS.MD_GRID_CLASSIFICATION_SETTINGS;

            PopulateGridTables(mColumnSettingsTable, mDColumnSettingsTable, fileName);
            PopulateGridTables(mRelationSettingsTable, mDRelationSettingsTable, fileName);
            PopulateGRID_SETTING(mGridSettingDataTable, mDGridSettingDataTable, fileName);
            PopulateGridTables(mtClassificationSettings, mDtClassificationSettings, fileName);
            mGridConfigSettingsDS.AcceptChanges();
        }


        private static void PopulateGRID_SETTING(DataTable mSourceTable, DataTable mDestinationTable, string fileName)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("MD_GRID_SETTING...");
            try
            {
                if (mSourceTable.Rows.Count > 0)
                {
                    foreach (DataRow mSourceRow in mSourceTable.Rows)
                    {
                        DataRow mDestinationRow = mDestinationTable.NewRow();
                        mDestinationRow["APP_CODE"] = mAppCode;
                        mDestinationRow["CONFIG_NAME"] = fileName;
                        foreach (DataColumn mSourceColumn in mSourceTable.Columns)
                        {
                            if (mDestinationTable.Columns.Contains(mSourceColumn.ColumnName))
                            {
                                if (mSourceRow[mSourceColumn.ColumnName] is bool)
                                {
                                    mDestinationRow[mSourceColumn.ColumnName] =
                                        (bool)mSourceRow[mSourceColumn.ColumnName] ? "S" : "N";
                                }
                                else
                                {
                                    mDestinationRow[mSourceColumn.ColumnName] = mSourceRow[mSourceColumn.ColumnName];
                                }
                            }
                        }

                        mDestinationTable.Rows.Add(mDestinationRow);
                    }
                }
                else
                {
                    DataRow mDestinationRow = mDestinationTable.NewRow();
                    mDestinationRow["APP_CODE"] = mAppCode;
                    mDestinationRow["CONFIG_NAME"] = fileName;
                    mDestinationTable.Rows.Add(mDestinationRow);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Errore: " + ex.Message + ex.Source);
            }
        }


        private static void PopulateGridTables(DataTable mSourceTable, DataTable mDestinationTable, string fileName)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(mDestinationTable.TableName + "...");
            try
            {
                if (mSourceTable.Rows.Count > 0)
                {
                    foreach (DataRow mSourceRow in mSourceTable.Rows)
                    {
                        DataRow mDestinationRow = mDestinationTable.NewRow();
                        mDestinationRow["APP_CODE"] = mAppCode;
                        mDestinationRow["CONFIG_NAME"] = fileName;
                        foreach (DataColumn mSourceColumn in mSourceTable.Columns)
                        {
                            if (mDestinationTable.Columns.Contains(mSourceColumn.ColumnName))
                            {
                                if (mSourceRow[mSourceColumn.ColumnName] is bool)
                                {
                                    mDestinationRow[mSourceColumn.ColumnName] =
                                        (bool)mSourceRow[mSourceColumn.ColumnName] ? "S" : "N";
                                }
                                else
                                {
                                    mDestinationRow[mSourceColumn.ColumnName] = mSourceRow[mSourceColumn.ColumnName];
                                }
                            }
                        }

                        mDestinationTable.Rows.Add(mDestinationRow);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Errore: " + ex.Message + ex.Source);
            }
        }

        private static void SaveToDatabase()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n*******************************");
            Console.WriteLine("Salvataggio in corso...");

            DataStoreConfig mDataStoreConfig = new DataStoreConfig();
            mDataStoreConfig = FetchDataProviderConfig.InitDataStoreConfig();

            //var mDataStore = DataStore.Factory.DataStoreFactory.GetDataStoreByDataProviderID("DEFAULT_DATA");
            var mDataStore = DataStore.Factory.DataStoreFactory.GetDataStore("SQL_SERVER", mDataStoreConfig);

            try
            {
                mDataStore.InsertUpdateData(mGridConfigSettingsDS.MD_GRID_CLASSIFICATION_SETTINGS);
                mDataStore.InsertUpdateData(mGridConfigSettingsDS.MD_GRID_SETTING);
                mDataStore.InsertUpdateData(mGridConfigSettingsDS.MD_GRID_RELATION_SETTING);
                mDataStore.InsertUpdateData(mGridConfigSettingsDS.MD_GRID_COLUMN_SETTING);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Salvataggio completato.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Errore: " + ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Rilanciare il programma?  S/N");
                var response = Console.ReadLine();
                if (response.ToUpper() == "S")
                {
                    Main(null);
                }
            }



        }


    }

}