using System.Collections.Generic;
using System.Text;
using Game.Collectibles.Pets;
using Game.Save;

namespace Game.UI.ViewModels
{
    public sealed class PetRowViewModel
    {
        public string InstanceId;
        public string PetId;
        public string DisplayName;
        public PetQuality Quality;
        public int Level;
        public int Count;
    }

    /// <summary>Read-only snapshot for pet hatch / list UI.</summary>
    public sealed class PetScreenViewModel
    {
        public List<PetRowViewModel> Rows = new List<PetRowViewModel>();
        public long Coins;

        public static PetScreenViewModel Build()
        {
            var vm = new PetScreenViewModel();
            if (SaveService.Data != null)
                vm.Coins = SaveService.Data.coins;

            foreach (var row in PetService.GetOwnedInstancesSorted())
            {
                var def = row.Def;
                vm.Rows.Add(new PetRowViewModel
                {
                    InstanceId = row.InstanceId,
                    PetId = row.PetId,
                    DisplayName = def != null ? def.displayName : row.PetId,
                    Quality = def != null ? def.quality : PetQuality.Green,
                    Level = def != null ? def.level : 1,
                    Count = 1,
                });
            }
            return vm;
        }

        public string ToListText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Pets (equipped first, then by glide coin bonus):");
            foreach (var r in Rows)
            {
                sb.AppendLine($"- [{r.Quality}] Lv{r.Level} {r.DisplayName} ({r.PetId}) x{r.Count}");
            }
            sb.AppendLine();
            sb.AppendLine($"Coins: {Coins}");
            return sb.ToString();
        }
    }
}
