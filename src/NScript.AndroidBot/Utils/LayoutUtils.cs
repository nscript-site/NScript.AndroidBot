using System;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    public class LayoutUtils
    {
        internal static String ClearXmlContent(String content)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = true;
                doc.LoadXml(content);
                if(doc.ChildNodes != null)
                {
                    foreach (XmlNode item in doc.ChildNodes)
                    {
                        ClearAttributes(item);
                        ClearChildNodes(item);
                    }
                }
                return doc.OuterXml;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return content;
        }
        static void ClearChildNodes(XmlNode node)
        {
            if (node == null || node.ChildNodes == null) return;
            List<XmlNode> removedList = new List<XmlNode>();
            foreach (XmlNode item in node.ChildNodes)
            {
                ClearChildNodes(item);
                if (IsNodeContainsText(item) == false)
                    removedList.Add(item);
            }

            foreach (XmlNode item in removedList)
            {
                String rid = GetResourceId(item);
                // Console.WriteLine(rid);
                node.RemoveChild(item);
            }
        }

        static String GetResourceId(XmlNode node)
        {
            if (node.Attributes != null)
            {
                String resId = node.Attributes["resource-id"].Value;
                return resId;
            }
            return String.Empty;
        }

        static bool IsNodeContainsText(XmlNode node)
        {
            if (node == null) return false;

            String rid = GetResourceId(node);

            if (node.Attributes != null)
            {
                foreach (XmlAttribute item in node.Attributes)
                {
                    bool match = false;
                    switch (item.Name)
                    {
                        case "text":
                        case "content-desc":
                            if (item.Value != null)
                            {
                                var val = item.Value.Trim();
                                if (String.IsNullOrEmpty(val) == false)
                                    match = true;
                            }
                            break;
                        default:
                            break;
                    }
                    if (match == true) return true;
                }
            }

            if (node.ChildNodes != null)
            {
                foreach (XmlNode child in node.ChildNodes)
                    if (IsNodeContainsText(child)) return true;
            }

            return false;
        }

        static void ClearAttributes(XmlNode node)
        {
            if (node == null) return;

            if (node.Attributes != null)
            {
                List<XmlAttribute> removeAttributes = new List<XmlAttribute>();
                foreach (XmlAttribute item in node.Attributes)
                {
                    bool match = false;
                    switch (item.Name)
                    {
                        case "text":
                        case "content-desc":
                        case "resource-id":
                        case "bounds":
                            match = true;
                            break;
                        default:
                            break;
                    }
                    if (match == false)
                        removeAttributes.Add(item);
                }
                foreach (var item in removeAttributes)
                    node.Attributes.Remove(item);
            }

            if (node.ChildNodes != null)
            {
                foreach (XmlNode child in node.ChildNodes)
                    ClearAttributes(child);
            }
        }
    }
}
