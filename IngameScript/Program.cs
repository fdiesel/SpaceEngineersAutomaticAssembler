using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        Dictionary<string, string> componentItemBlueprintMap = new Dictionary<string, string>
        {
            { "Construction", "ConstructionComponent"},
            { "Computer", "ComputerComponent"},
            { "Detector", "DetectorComponent"},
            { "Explosive", "ExplosiveComponent"},
            { "Girder", "GirderComponent"},
            { "GravityGenerator", "GravityGeneratorComponent"},
            { "Medical", "MedicalComponent"},
            { "Motor", "MotorComponent"},
            { "Reactor", "ReactorComponent"},
            { "RadioCommunication", "RadioCommunicationComponent"},
            { "Thrust", "ThrustComponent"},
        };

        IMyAssembler Assembler;
        List<IMyCargoContainer> Containers;
        List<IMyTextPanel> TextPanels;

        string BlueprintIdBaseString;

        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        private void debugInfoInventory()
        {
            var outText = "";
            foreach (IMyEntity container in Containers)
            {
                var items = new List<MyInventoryItem>();
                container.GetInventory().GetItems(items);
                foreach (var item in items)
                {
                    outText += item.ToString() + "\r\n";
                }
            }
            foreach (IMyTextPanel textPanel in TextPanels)
            {
                textPanel.WriteText(outText);
            }
        }
        private void debugInfoQueue()
        {
            var outText = "";
            var items = new List<MyProductionItem>();
            Assembler.GetQueue(items);
            foreach (var item in items)
            {
                outText += item.BlueprintId.ToString() + "\r\n";
            }
            foreach (IMyTextPanel textPanel in TextPanels)
            {
                textPanel.WriteText(outText);
            }
        }

        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.

            string className = "[AUTO]";

            BlueprintIdBaseString = "MyObjectBuilder_BlueprintDefinition/";

            // Assembler
            List<IMyAssembler> assemblers = new List<IMyAssembler>();
            GridTerminalSystem.GetBlocksOfType(assemblers);
            Assembler = assemblers.Find(assembler => assembler.CustomName.EndsWith(className));
            Echo("Found Assembler \"" + Assembler.CustomName + "\"");

            // Containers
            Containers = new List<IMyCargoContainer>();
            GridTerminalSystem.GetBlocksOfType(Containers);
            Containers = Containers.FindAll(container => container.CustomName.EndsWith(className));
            Echo("Found " + Containers.Count.ToString() + " Containers");

            // Text Panels
            TextPanels = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(TextPanels);
            TextPanels = TextPanels.FindAll(textPanel => textPanel.CustomName.EndsWith(className));
            Echo("Found " + TextPanels.Count.ToString() + " Text Panels");
            foreach (IMyTextPanel textPanel in TextPanels)
            {
                textPanel.ContentType = ContentType.TEXT_AND_IMAGE;
                Echo("Setup panel \"" + textPanel.CustomName + "\"");
            }

            // Runtime (about 1 Tick probably about 1ms)
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.

            //debugInfoQueue();
            //return;

            List<MyProductionItem> assemblerQueue = new List<MyProductionItem>();
            Assembler.GetQueue(assemblerQueue);

            foreach (IMyTextPanel textPanel in TextPanels)
            {

                string textPanelText = "";

                string customData = textPanel.CustomData;

                string[] customDataLines = customData.Split('\n');

                foreach (string line in customDataLines)
                {

                    string[] lineSegments = line.Trim().Split(' ');

                    if (lineSegments.Length == 3)
                    {

                        try
                        {
                            // parse line segments

                            string itemName = lineSegments[0];
                            int lowerBound = int.Parse(lineSegments[1]);
                            int upperBound = int.Parse(lineSegments[2]);

                            // count components available in all containers and the assembler

                            int count = 0;
                            foreach (IMyCargoContainer container in Containers)
                            {
                                count += GetItemCountFromInventory(container.GetInventory(), itemName);
                            }
                            count += GetItemCountFromInventory(Assembler.OutputInventory, itemName);

                            // add difference in between count and upper bound to assembler queue if count is smaller than lower bound

                            if (count < lowerBound)
                            {
                                string blueprintName;
                                if (!componentItemBlueprintMap.TryGetValue(itemName, out blueprintName))
                                    blueprintName = itemName;

                                AddToAssemblerQueueIfNotPresent(assemblerQueue, blueprintName, upperBound - count);
                            }

                            textPanelText += itemName + " " + count.ToString() + "\n";

                        }
                        catch (Exception e)
                        {
                            Echo("An error occurred during script execution.");
                            Echo($"Exception: {e}\n---");
                        }

                    }

                }

                textPanel.WriteText(textPanelText);

            }

        }

        public int GetItemCountFromInventory(IMyInventory inventory, string itemName)
        {
            MyDefinitionId myDefinitionId = GetMyDefinitionIdByName(itemName);
            MyItemType myItemType = MyItemType.MakeComponent(myDefinitionId.SubtypeId.ToString());
            return (int)inventory.GetItemAmount(myItemType);
        }

        public void AddToAssemblerQueueIfNotPresent(List<MyProductionItem> assemblerQueue, string itemName, int amount)
        {
            if (!IsInAssemblerQueue(assemblerQueue, itemName))
            {
                AddToAssemblerQueue(itemName, amount);
            }
        }

        public void AddToAssemblerQueue(string itemName, int amount)
        {
            MyDefinitionId blueprint = GetMyDefinitionIdByName(itemName);
            Assembler.AddQueueItem(blueprint, (double)amount);
        }

        public bool IsInAssemblerQueue(List<MyProductionItem> queue, string itemName)
        {
            return queue.FindIndex(item => item.BlueprintId.ToString() == BlueprintIdBaseString + itemName) != -1;
        }

        // !!! Only handles components as they can be produced by the assembler (MakeComponent) !!!
        public MyItemType GetMyItemTypeByMyDefinitionId(MyDefinitionId myDefinitionId)
        {
            return MyItemType.MakeComponent(myDefinitionId.SubtypeId.ToString());
        }

        public MyDefinitionId GetMyDefinitionIdByName(string name)
        {
            return MyDefinitionId.Parse(BlueprintIdBaseString + name);
        }
    }
}
