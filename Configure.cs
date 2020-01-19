using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Android_Transfer_Protocol.Configure
{
    static class Configurer
    {
        public static Conf conf;
        private const string filename = "conf.xml";
        private static readonly DataContractSerializer serializer = new DataContractSerializer(typeof(Conf));
        public static void Read()
        {
            try
            {
                FileStream fs = new FileStream(filename, FileMode.Open);
                try
                {
                    conf = (Conf)serializer.ReadObject(fs);
                    if (conf == null) conf = new Conf();
                    if (conf.Show == null) conf.Show = new ShowProp();
                    if (conf.Show.ColHeaderConf == null) conf.Show.ColHeaderConf = new Dictionary<string, ColHeaderProp>();
                    if (conf.Show.ToolBarConf == null) conf.Show.ToolBarConf = new Dictionary<string, ToolBarProp>();
                    if (conf.Show.WindowConf == null) conf.Show.WindowConf = new Dictionary<string, WindowProp>();
                }
                catch (SerializationException e)
                {
                    fs.Close();
                    Save();
                    _ = e;
                }
                fs.Close();
            }
            catch (FileNotFoundException e)
            {
                Save();
                _ = e;
            }
            catch (IOException e)
            {
                _ = e;
            }
        }

        public static void Save()
        {
            try
            {
                if (conf == null) conf = new Conf();
                XmlWriter writer = XmlWriter.Create(filename, new XmlWriterSettings { Indent = true });
                serializer.WriteObject(writer, conf);
                writer.Close();
            }
            catch (IOException e)
            {
                _ = e;
                throw;
            }
        }
    }
    public class Conf
    {
        public ShowProp Show = new ShowProp();
    }
    public class ShowProp
    {
        public Dictionary<string, WindowProp> WindowConf = new Dictionary<string, WindowProp>();
        public Dictionary<string, ToolBarProp> ToolBarConf = new Dictionary<string, ToolBarProp>();
        public Dictionary<string, ColHeaderProp> ColHeaderConf = new Dictionary<string, ColHeaderProp>();
    }

    public class ColHeaderProp
    {
        public int index = 0;
        public bool Visible = true;
    }

    public class WindowProp
    {
        public double Width = 800;
        public double Height = 600;
    }

    public class ToolBarProp
    {
        public int Band = 0;
        public int BandIndex = 0;
        public bool Visible = false;
    }

}