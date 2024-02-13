using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridConfig.Interface
{
    [Serializable]
    public class GridColumnConfig
    {
        public string DATA_FIELD_NAME;
        public string TYPE;
        public string CAPTION;
        public int LENGTH;
        public bool VISIBLE;
        public bool DISABLED;
        public bool EDITABLE;
        public bool NULLABLE;
        public string FORMAT;
        public string WIDTH;
        public bool IS_LOOKUP;
        public bool IS_DOMAIN;
        public string DOMAIN;
        public string SUB_TYPE;
        public bool USE_AUTOCOMPLETE;
        public int POSITION;
        public string DEFAULT_VALUE;
        public string DEF_VALUE_GEOM_DEP_PARAMS;

        public string LOOKUP_MANAGER_NAME;
        public string LOOKUP_TABLE_NAME;
        public string LOOKUP_KEY_FIELD;
        public string LOOKUP_TEXT_FIELD;
        public string LOOKUP_FILTER;
        public bool FILTERABLE;

        public string FK_FIELD;
        public string REL_FK_FIELD;
        public string REL_TABLE_NAME;
        public string REL_TABLE_FILTER;

        public string DEPENDENCY_FIELD;
        public string DEPENDENCY_TABLE;
        public string DEPENDENCY_VALUE_FIELD;
        public string DEPENDENCY_MAP_VALUE_FIELD;

        public string JSONPART_JSON_FIELD;

        public bool SHOW_IN_SUMMARY;
        public bool SHOW_IN_INFO;

        public string HOT_LINK_TEXT;
        public string HOT_LINK_TYPE;
        public string HOT_LINK_PARAMS;
        public string HOT_LINK_GROUP;
        [Obsolete("Utilizzare al suo posto HOT_LINK_CONDITION_EXPRESSION")]
        public string HOT_LINK_CONDITION_FIELD;
        public string HOT_LINK_CONDITION_EXPRESSION;

        public string CLASSIFICATION_ID;
        public string CLASSIFICATION_FIELD;

        public string HELP_MSG;
        public string URL;

        public string GROUP;
        public string CATEGORY;

        public bool IS_UNIQUE;

        /// <summary>
        /// Contiene eventuali valori e descrizioni di lookup per la colonna in questione.
        /// </summary>
        public List<LookupItem> LookupInfo = new List<LookupItem>();

        /// <summary>
        /// Dizionario contenente, per ogni valore della colonna corrente, i valori corrispondenti delle colonne che dipendono da essa.
        /// Da mettere nelle colonne che dipendono a qualcuno
        /// </summary>
        public Dictionary<string, List<object>> DependencyDictionary = new Dictionary<string, List<object>>();

        /// <summary>
        /// Dizionario contenente i campi che dipendono da un certo campo
        /// Da mettere nelle colonne da cui dipende qualcuno
        /// </summary>
        public Dictionary<string, List<string>> DependencyFieldsDict = new Dictionary<string, List<string>>();

        public GridColumnConfig Clone()
        {
            GridColumnConfig clonedColConfig = new GridColumnConfig();

            clonedColConfig.DATA_FIELD_NAME = DATA_FIELD_NAME;
            clonedColConfig.TYPE = TYPE;
            clonedColConfig.CAPTION = CAPTION;
            clonedColConfig.LENGTH = LENGTH;
            clonedColConfig.VISIBLE = VISIBLE;
            clonedColConfig.EDITABLE = EDITABLE;
            clonedColConfig.NULLABLE = NULLABLE;
            clonedColConfig.FORMAT = FORMAT;
            clonedColConfig.WIDTH = WIDTH;
            clonedColConfig.IS_LOOKUP = IS_LOOKUP;
            clonedColConfig.IS_DOMAIN = IS_DOMAIN;
            clonedColConfig.DOMAIN = DOMAIN;
            clonedColConfig.SUB_TYPE = SUB_TYPE;
            clonedColConfig.USE_AUTOCOMPLETE = USE_AUTOCOMPLETE;
            clonedColConfig.DEFAULT_VALUE = DEFAULT_VALUE;
            clonedColConfig.DEF_VALUE_GEOM_DEP_PARAMS = DEF_VALUE_GEOM_DEP_PARAMS;
            clonedColConfig.LOOKUP_MANAGER_NAME = LOOKUP_MANAGER_NAME;
            clonedColConfig.LOOKUP_TABLE_NAME = LOOKUP_TABLE_NAME;
            clonedColConfig.LOOKUP_KEY_FIELD = LOOKUP_KEY_FIELD;
            clonedColConfig.LOOKUP_TEXT_FIELD = LOOKUP_TEXT_FIELD;
            clonedColConfig.LOOKUP_FILTER = LOOKUP_FILTER;
            clonedColConfig.FILTERABLE = FILTERABLE;

            clonedColConfig.DEPENDENCY_FIELD = DEPENDENCY_FIELD;
            clonedColConfig.DEPENDENCY_TABLE = DEPENDENCY_TABLE;
            clonedColConfig.DEPENDENCY_VALUE_FIELD = DEPENDENCY_VALUE_FIELD;
            clonedColConfig.DEPENDENCY_MAP_VALUE_FIELD = DEPENDENCY_MAP_VALUE_FIELD;

            clonedColConfig.JSONPART_JSON_FIELD = JSONPART_JSON_FIELD;

            clonedColConfig.FK_FIELD = FK_FIELD;
            clonedColConfig.REL_FK_FIELD = REL_FK_FIELD;
            clonedColConfig.REL_TABLE_NAME = REL_TABLE_NAME;
            clonedColConfig.REL_TABLE_FILTER = REL_TABLE_FILTER;
            clonedColConfig.SHOW_IN_SUMMARY = SHOW_IN_SUMMARY;
            clonedColConfig.SHOW_IN_INFO = SHOW_IN_INFO;
            clonedColConfig.LookupInfo = new List<LookupItem>(LookupInfo);
            clonedColConfig.DependencyDictionary = new Dictionary<string, List<object>>(DependencyDictionary);
            clonedColConfig.DependencyFieldsDict = new Dictionary<string, List<string>>(DependencyFieldsDict);
            clonedColConfig.HOT_LINK_TEXT = HOT_LINK_TEXT;
            clonedColConfig.HOT_LINK_TYPE = HOT_LINK_TYPE;
            clonedColConfig.HOT_LINK_PARAMS = HOT_LINK_PARAMS;
            clonedColConfig.HOT_LINK_GROUP = HOT_LINK_GROUP;
            clonedColConfig.HOT_LINK_CONDITION_FIELD = HOT_LINK_CONDITION_FIELD;
            clonedColConfig.HOT_LINK_CONDITION_EXPRESSION = HOT_LINK_CONDITION_EXPRESSION;
            clonedColConfig.DISABLED = DISABLED;
            clonedColConfig.POSITION = POSITION;

            clonedColConfig.CLASSIFICATION_ID = CLASSIFICATION_ID;
            clonedColConfig.CLASSIFICATION_FIELD = CLASSIFICATION_FIELD;


            clonedColConfig.HELP_MSG = HELP_MSG;
            clonedColConfig.URL = URL;

            clonedColConfig.CATEGORY = CATEGORY;
            clonedColConfig.GROUP = GROUP;

            clonedColConfig.IS_UNIQUE = IS_UNIQUE;

            return clonedColConfig;
        }
    }

    [Serializable]
    public class LookupItem
    {
        public object Value;
        public string Description;
    }


}
