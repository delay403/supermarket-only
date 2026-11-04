/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Loading
{
    public partial class UItipsComp : GComponent
    {
        public GTextField m_context;
        public const string URL = "ui://69318iqyqu8ncg";

        public static UItipsComp CreateInstance()
        {
            return (UItipsComp)UIPackage.CreateObject("Loading", "tipsComp");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_context = (GTextField)GetChildAt(1);
        }
    }
}