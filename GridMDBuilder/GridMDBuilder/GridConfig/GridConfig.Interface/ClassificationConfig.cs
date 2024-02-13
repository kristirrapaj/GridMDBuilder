using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridConfig.Interface
{
    [Serializable]
    public class ClassificationConfig
    {
        public string ID;
        public string TYPE;

        public List<ClassDef> ClassificationDefs = new List<ClassDef>();

        public ClassificationConfig Clone()
        {
            ClassificationConfig clonedConfig = new ClassificationConfig();

            clonedConfig.ID = ID;
            clonedConfig.TYPE = TYPE;

            foreach (ClassDef classDef in ClassificationDefs)
            {
                clonedConfig.ClassificationDefs.Add(new ClassDef() { Template = classDef.Template, Description = classDef.Description, Value = classDef.Value });
            }

            return clonedConfig;
        }
    }

    [Serializable]
    public class ClassDef
    {
        public string Value;
        public string Description;
        public string Template;
    }
}
