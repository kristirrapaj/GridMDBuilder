using System;
using System.Data;
using System.IO;

namespace GridMDBuilder
{
    class Program
    {
        private static string XmlPath = "";
        private static string AppCode = "";
        
        static void Main(string[] args)
        {
            //prendo le informazioni da riga di comando
            XmlPath = GetPath();
            AppCode = GetAppCode();
            
            //crea il dataset per ciascun file di configurazione
            string[] XmlFiles = GetFiles(XmlPath);
            
            foreach (string file in XmlFiles)
            {
                Console.WriteLine("Creazione dataset per il file: " + file);
                BuildDataSet(file);
            }
        }

        private static string GetPath()
        {
            Console.WriteLine("Inserisci i path dei files di congiurazione XML: /n");
            return Console.ReadLine();
        }
        
        private static string GetAppCode()
        {
            Console.WriteLine("Inserisci il codice dell'applicazione: /n");
            return Console.ReadLine();
        }

        private static string[] GetFiles(string path)
        {
            string[] files = Directory.GetFiles(path, "*.xml");
            return files;
        }
        
        private static void BuildDataSet(string file)
        {
            DataSet ds = new DataSet();
            ds.ReadXml(file);
            
            
        }
    }
}