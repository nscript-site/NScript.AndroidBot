using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using System.Xml;
    using System.Xml.XPath;
    using Geb.Image;

    public class PageLayout
    {
        private XmlDocument _doc;
        private XPathNavigator _nav;
        private String _content;

        public XPathNavigator Navigator { get { return _nav; } }
        public String Content { get { return _content; } }

        public PageLayout(String content)
        {
            _doc = new XmlDocument();
            _doc.LoadXml(content);
            _nav = _doc.CreateNavigator();
            _content = content;
        }

        public XPathNodeIterator Select(String xpath)
        {
            return _nav.Select(xpath);
        }

        public bool Contains(String xpath)
        {
            return Select(xpath).MoveNext();
        }

        public XPathNavigator First(String xpath)
        {
            var it = _nav.Select(xpath);
            if (it.MoveNext() == true)
                return it.Current;
            else
                return null;
        }

        public List<XPathNavigator> All(String xpath)
        {
            List<XPathNavigator> list = new List<XPathNavigator>();
            var it = _nav.Select(xpath);
            while(it.MoveNext())
            {
                list.Add(it.Current);
            }
            return list;
        }


    }
}
