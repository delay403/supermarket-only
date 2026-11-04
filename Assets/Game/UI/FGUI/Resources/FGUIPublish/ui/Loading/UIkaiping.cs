/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Loading
{
    public partial class UIkaiping : GComponent
    {
        public GTextField m_author;
        public GTextField m_authorId;
        public const string URL = "ui://69318iqynasge";

        public static UIkaiping CreateInstance()
        {
            return (UIkaiping)UIPackage.CreateObject("Loading", "kaiping");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_author = (GTextField)GetChildAt(1);
            m_authorId = (GTextField)GetChildAt(2);
        }
    }
}