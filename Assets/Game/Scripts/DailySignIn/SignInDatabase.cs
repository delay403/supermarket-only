using System.Collections.Generic;
using UnityEngine;

namespace Game.DailySignIn
{
    [CreateAssetMenu(menuName = "Game/DailySignIn/SignInDatabase", fileName = "SignInDatabase")]
    public sealed class SignInDatabase : ScriptableObject
    {
        public List<SignInDayDef> days = new List<SignInDayDef>();
    }
}

