using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace TurboTad
{
    // Device discovery is an optional catalog of controls physically exercised
    // by the player. It never replaces mapping.xml or the authoritative device
    // APIs. Unscanned controls stay available as fallbacks.
    internal sealed class InputDiscoveryCatalog
    {
        private readonly List<InputSource> sources = new List<InputSource>();
        private readonly HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
        internal string LoadIssue;
        internal IList<InputSource> Sources { get { return sources.AsReadOnly(); } }
        internal int Count { get { return sources.Count; } }

        internal bool Add(InputSource source)
        {
            if (source == null) return false;
            bool analog = source.Kind == "axis" || source.Kind == "xaxis";
            if (!source.Valid(analog) || !identities.Add(source.Identity)) return false;
            sources.Add(source);
            return true;
        }

        internal bool Contains(InputSource source)
        { return source != null && identities.Contains(source.Identity); }

        internal IEnumerable<InputSource> OfType(bool analog)
        {
            foreach (InputSource source in sources)
            {
                bool isAnalog = source.Kind == "axis" || source.Kind == "xaxis";
                if (analog == isAnalog) yield return source;
            }
        }

        private static string FilePath(string root)
        { return Path.Combine(root, "device-discovery.xml"); }

        internal static InputDiscoveryCatalog Load(string root)
        {
            InputDiscoveryCatalog result = new InputDiscoveryCatalog();
            string path = FilePath(root);
            if (!File.Exists(path)) return result;
            try
            {
                // A discovery inventory may be edited or corrupted independently
                // of saved flight mappings. Refuse DTDs and oversized XML.
                XmlReaderSettings settings = new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 65536,
                    MaxCharactersFromEntities = 1024
                };
                XmlDocument doc = new XmlDocument();
                doc.XmlResolver = null;
                using (XmlReader reader = XmlReader.Create(path, settings))
                    doc.Load(reader);
                XmlElement catalog = doc.DocumentElement;
                if (catalog == null || catalog.Name != "DeviceDiscovery" ||
                    catalog.GetAttribute("version") != "1") throw new FormatException("Unsupported discovery file version");
                foreach (XmlNode node in catalog.ChildNodes)
                {
                    XmlElement e = node as XmlElement;
                    if (e == null || e.Name != "Source") continue;
                    string kind = e.GetAttribute("kind");
                    InputSource source = InputMapper.ReadSource(e, kind == "axis" || kind == "xaxis");
                    if (source == null || !result.Add(source)) throw new FormatException("Duplicate or invalid discovery entry");
                }
            }
            catch (Exception e)
            {
                // Never silently apply a partial inventory from a corrupt scan.
                result = new InputDiscoveryCatalog();
                result.LoadIssue = e.Message;
            }
            return result;
        }

        internal void Save(string root)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement catalog = doc.CreateElement("DeviceDiscovery");
            catalog.SetAttribute("version", "1");
            doc.AppendChild(catalog);
            foreach (InputSource source in sources)
                catalog.AppendChild(InputMapper.WriteSource(doc, "Source", source));
            string path = FilePath(root);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            Directory.CreateDirectory(root);
            try
            {
                doc.Save(temp);
                // Replace the completed file atomically. A failed write must
                // not leave a half-written inventory for the next launch.
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch {} }
        }
    }
}
