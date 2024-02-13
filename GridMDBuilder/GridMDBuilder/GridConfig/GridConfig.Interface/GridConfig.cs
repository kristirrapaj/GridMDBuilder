using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridConfig.Interface
{
    [Serializable]
    public class GridConfig
    {
        public string ConfigName;

        public string PKField;

        public bool UseCache;

        public string Alias;

        /// <summary>
        /// Indica se gli allegati sono abilitati o meno.
        /// </summary>
        public bool EnableAttachments;

        /// <summary>
        /// Nome del campo contenente la geometria. Se non ha geometrie il campo è null.
        /// </summary>
        public string GeometryField;

        /// <summary>
        /// Indica se gestire le sottogriglie.
        /// </summary>
        public bool UseSubgrid;
        
        /// <summary>
        /// Indica la configurazione da utilizzare per le sottogriglie.
        /// </summary>
        public string SubgridConfig;

        /// <summary>
        /// Indica il campo della griglia principale da utilizzare per filtrare nella sottogriglia.
        /// </summary>
        public string SubgridParentField;

        /// <summary>
        /// Indica il campo nella sottogriglia sul quale applicare il filtro.
        /// </summary>
        public string SubgridChildField;

        /// <summary>
        /// Indica il campo che contiene l'informazione sulla presenza o meno di figli.
        /// </summary>
        public string HasChildrenField;

        public decimal? RefreshRate;

        public List<GridColumnConfig> ColumnsConfig = new List<GridColumnConfig>();

        public List<RelationConfig> RelationsConfig = new List<RelationConfig>();

        public FlagsOperationInfoDTO FOpInfo;

        public List<ClassificationConfig> ClassificationConfigs = new List<ClassificationConfig>();

        /// <summary>
        /// Dizionario contenente, i valori delle dipendenze tra le colonne.
        /// I dizionario è formato con chiave il nome dei campi dipendenti, 
        /// e valore un dizionario contente i valori del campo padre come chiave e la lista dei valori corrispondenti per i figli come valore
        /// </summary>
        //public Dictionary<string, Dictionary<string, List<object>>> DependencyDictionary = new Dictionary<string, Dictionary<string, List<object>>>();


        /// <summary>
        /// Restituisce una copia dell'oggetto.
        /// </summary>
        /// <returns></returns>
        public GridConfig Clone()
        {
            GridConfig clonedConfig = new GridConfig();

            clonedConfig.ConfigName = ConfigName;
            clonedConfig.PKField = PKField;
            clonedConfig.GeometryField = GeometryField;
            clonedConfig.FOpInfo = FOpInfo;
            clonedConfig.UseCache = UseCache;
            clonedConfig.Alias = Alias;
            clonedConfig.EnableAttachments = EnableAttachments;
            //clonedConfig.DependencyDictionary = DependencyDictionary;

            if (ColumnsConfig != null)
            {
                List<GridColumnConfig> clonedColumnsConfig = new List<GridColumnConfig>();

                foreach (GridColumnConfig colConfig in ColumnsConfig)
                {
                    clonedColumnsConfig.Add(colConfig.Clone());
                }

                clonedConfig.ColumnsConfig = clonedColumnsConfig;
            }

            foreach (RelationConfig relConfig in RelationsConfig)
            {
                clonedConfig.RelationsConfig.Add(relConfig);
            }

            if (ClassificationConfigs != null)
            {
                List<ClassificationConfig> clonedClassConfigs = new List<ClassificationConfig>();

                foreach (ClassificationConfig classConfig in ClassificationConfigs)
                {
                    clonedClassConfigs.Add(classConfig.Clone());
                }

                clonedConfig.ClassificationConfigs = clonedClassConfigs;
            }

            clonedConfig.FOpInfo = FOpInfo;

            clonedConfig.UseSubgrid = UseSubgrid;
            clonedConfig.SubgridConfig = SubgridConfig;
            clonedConfig.SubgridParentField = SubgridParentField;
            clonedConfig.SubgridChildField = SubgridChildField;
            clonedConfig.HasChildrenField = HasChildrenField;
            clonedConfig.RefreshRate = RefreshRate;

            return clonedConfig;
        }

    }

}
