using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridConfig.Interface
{
    [Serializable]
    public class RelationConfig
    {
        /// <summary>
        /// Nome del campo della tabella padre al quale punta la chiave esterna della tabella figlio.
        /// </summary>
        public string PARENT_KEY_FIELD;

        /// <summary>
        /// Nome della tabella figlio.
        /// </summary>
        public string REL_CHILD_TABLE_NAME;

        /// <summary>
        /// Nome dela campo della tabella figlio contenente la chiave esterna.
        /// </summary>
        public string REL_CHILD_FK_FIELD;

        /// <summary>
        /// Testo da visualizzare per la relazione.
        /// </summary>
        public string REL_CAPTION;

        /// <summary>
        /// Eventuale campo da utilizzare per visualizzare le relazioni.
        /// </summary>
        public string LINK_FIELD;
    }

}
