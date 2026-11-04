using Game.Audio;
using Game.VFX;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    public static class SupermarketOrganizePresentationFx
    {
        public static void PlaySelect(Vector3 worldPos)
        {
            AudioManager.Instance?.PlaySfx3D(SfxId.SupermarketOrganizeSelect, worldPos);
        }

        public static void PlayPickUp(Vector3 worldPos)
        {
            AudioManager.Instance?.PlaySfx3D(SfxId.SupermarketOrganizePickUp, worldPos);
            GameplayVfxManager.Instance?.BorrowWorld(
                VfxKind.SupermarketOrganizePickUp, worldPos, Quaternion.identity, Vector3.one, -1f);
        }

        public static void PlayPlace(Vector3 worldPos)
        {
            AudioManager.Instance?.PlaySfx3D(SfxId.SupermarketOrganizePlace, worldPos);
            GameplayVfxManager.Instance?.BorrowWorld(
                VfxKind.SupermarketOrganizePlace, worldPos, Quaternion.identity, Vector3.one, -1f);
        }

        public static void PlayPlaceWrong(Vector3 worldPos)
        {
            AudioManager.Instance?.PlaySfx3D(SfxId.SupermarketOrganizePlaceWrong, worldPos);
        }

        public static void PlayShelfComplete(Vector3 worldPos)
        {
            AudioManager.Instance?.PlaySfx3D(SfxId.SupermarketOrganizeShelfComplete, worldPos);
            GameplayVfxManager.Instance?.BorrowWorld(
                VfxKind.SupermarketOrganizeShelfComplete, worldPos, Quaternion.identity, Vector3.one, -1f);
        }

        public static void PlayVictory()
        {
            AudioManager.Instance?.PlaySfx(SfxId.SettlementWin);
            GameplayVfxManager.Instance?.BorrowWorld(
                VfxKind.SupermarketOrganizeVictory, Vector3.zero, Quaternion.identity, Vector3.one, -1f);
        }
    }
}
