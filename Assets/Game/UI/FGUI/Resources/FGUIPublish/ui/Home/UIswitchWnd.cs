/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Home
{
    public partial class UIswitchWnd : GComponent
    {
        public GImage m_mask;
        public Transition m_mask_Close;
        public Transition m_mask_Open;
        public const string URL = "ui://l851vxyyllnxnl";

        public static UIswitchWnd CreateInstance()
        {
            return (UIswitchWnd)UIPackage.CreateObject("Home", "switchWnd");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_mask = (GImage)GetChildAt(0);
            m_mask_Close = GetTransitionAt(0);
            m_mask_Open = GetTransitionAt(1);
        }
    }
}