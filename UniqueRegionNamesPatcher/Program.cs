using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using System;
using System.Threading.Tasks;
using UniqueRegionNamesPatcher.Extensions;
using UniqueRegionNamesPatcher.Utility;

namespace UniqueRegionNamesPatcher
{
    public class Program
    {
        private static Lazy<Settings> _lazySettings = null!;
        private static Settings Settings => _lazySettings.Value;

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "UniqueRegionNamesPatcher.esp")
                .SetAutogeneratedSettings("Settings", "settings.json", out _lazySettings, false)
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            Console.WriteLine("===================");

            UrnRegionMap coordMap = Settings.TamrielSettings.GetUrnRegionMap(ref state);
            long changeCount = 0;

            foreach (var cellContext in state.LoadOrder.PriorityOrder.Cell().WinningContextOverrides(state.LinkCache))
            {
                // filter out unwanted cells
                var cell = cellContext.Record;

                if (cell.Flags.HasFlag(Cell.Flag.IsInteriorCell))
                    continue; //< is an interior cell
                else if (cell.Grid is null)
                { // doesn't have a grid position:
                    Console.WriteLine($"Exterior cell '{cell.Name?.String ?? cell.EditorID ?? cell.FormKey.IDString()}' does not have a 'Grid' subrecord!");
                    continue;
                }
                else if (!cellContext.TryGetParent<IWorldspaceGetter>(out var worldspace) || !worldspace.FormKey.Equals(TamrielSettings.Worldspace.FormKey))
                { // isn't located in the Tamriel worldspace
                    continue;
                }
                else if (cell.Grid.Point.X.Equals(0) && cell.Grid.Point.Y.Equals(0) && cell.EditorID is null && cell.Name is null)
                { // is a persistent worldspace cell:
                    Console.WriteLine($"Skipping persistent worldspace cell '{cell.FormKey.IDString()}'");
                    continue;
                }

                // get regions:
                var regions = coordMap.GetFormLinksForPos(cell.Grid.Point);

                if (regions.Count.Equals(0))
                { // no regions matching this position:
                    Console.WriteLine($"No region data found for exterior cell '{cell.Name?.String ?? cell.EditorID ?? cell.FormKey.IDString()}' {cell.Grid.Point}.");
                    continue;
                }

                if (cell.Regions is null || !cell.Regions.ContainsAll(regions))
                {
                    var cellState = cellContext.GetOrAddAsOverride(state.PatchMod);

                    if (cellState.Regions is null)
                        cellState.Regions = new();

                    cellState.Regions.AddRangeIfUnique(regions);
                    ++changeCount;

                    Console.WriteLine($"Added {regions.Count} regions to exterior cell '{cell.Name?.String ?? cell.EditorID ?? cell.FormKey.IDString()}' {cell.Grid.Point}.");
                }
                else
                    Console.WriteLine($"No new regions to add to exterior cell '{cell.Name?.String ?? cell.EditorID ?? cell.FormKey.IDString()}' {cell.Grid.Point}");
            }

            if (changeCount.Equals(0))
                Console.WriteLine("No changes were made.");
            else
                Console.WriteLine($"Successfully modified {changeCount} cell{(changeCount.Equals(1) ? "" : "s")}.");

            Console.WriteLine("===================");
        }
    }
}
