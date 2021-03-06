using System;
using System.IO;

namespace ConfigurationParser
{
    public class FileReader 
    {
        public string FilePath { get; private set; }
        public string Contents { get; private set; }
        public string[] Lines { get; private set; }

        public FileReader (string filePath)
        {
            if (!File.Exists (filePath))
                throw new FileNotFoundException (String.Format ("File {0} does not exist", filePath));

            FilePath = filePath;
            using (var reader = new StreamReader(FilePath))
            {
                Contents = reader.ReadToEnd().Trim();
            	reader.Close();
            }
            Lines = Contents.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            
            
        }
        
    }
}

