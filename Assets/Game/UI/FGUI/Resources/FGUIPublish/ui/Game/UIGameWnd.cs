/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UIGameWnd : GComponent
    {
        public GButton m_BtnInteract;
        public UICtrpClimb m_BtnClimb;
        public UICtrpSpeed m_BtnAdSpeed;
        public UISlider m_SliderPower;
        public GButton m_RouterPet;
        public GButton m_RouterPerShop;
        public GButton m_RouterCha;
        public GButton m_RouterTrail;
        public GButton m_RouterWing;
        public GButton m_BtnSetting;
        public UIDiamond m_ItemDiamond;
        public UIMoney m_ItemMoney;
        public UIChapion m_ItemChapion;
        public GTextField m_HeightLabel;
        public GButton m_BtnSignin;
        public GButton m_BtnLoop;
        public const string URL = "ui://6laopsucnayu89";

        public static UIGameWnd CreateInstance()
        {
            return (UIGameWnd)UIPackage.CreateObject("Game", "GameWnd");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_BtnInteract = (GButton)GetChildAt(0);
            m_BtnClimb = (UICtrpClimb)GetChildAt(1);
            m_BtnAdSpeed = (UICtrpSpeed)GetChildAt(2);
            m_SliderPower = (UISlider)GetChildAt(3);
            m_RouterPet = (GButton)GetChildAt(4);
            m_RouterPerShop = (GButton)GetChildAt(5);
            m_RouterCha = (GButton)GetChildAt(6);
            m_RouterTrail = (GButton)GetChildAt(7);
            m_RouterWing = (GButton)GetChildAt(8);
            m_BtnSetting = (GButton)GetChildAt(9);
            m_ItemDiamond = (UIDiamond)GetChildAt(10);
            m_ItemMoney = (UIMoney)GetChildAt(11);
            m_ItemChapion = (UIChapion)GetChildAt(12);
            m_HeightLabel = (GTextField)GetChildAt(13);
            m_BtnSignin = (GButton)GetChildAt(14);
            m_BtnLoop = (GButton)GetChildAt(15);
        }
    }
}