using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Savior;
// herpppppp why dis in wrong assembly ;-;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

[assembly: MelonInfo(typeof(SeatExitController.SeatExitController), "SeatExitController", "1.0.0", "loukylor", "https://github.com/loukylor/CVR-Mods")]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]
namespace SeatExitController
{
    public class SeatExitController : MelonMod
    {
        public override void OnApplicationStart()
        {
            LoggerInstance.Msg("Initializing...");
            HarmonyInstance.Patch(
                typeof(CVRSeat).GetMethod(nameof(CVRSeat.Update)),
                transpiler: typeof(SeatExitController).GetMethod(nameof(SeatUpdateTranspiler), BindingFlags.NonPublic | BindingFlags.Static).ToNewHarmonyMethod()
            );
            LoggerInstance.Msg("Finished!");
        }

        private static IEnumerable<CodeInstruction> SeatUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> instrucList = new List<CodeInstruction>(instructions);
            List<CodeInstruction> result = new List<CodeInstruction>() { instrucList[0] };

            CodeInstruction lastInstruction = instrucList[0];
            
            FieldInfo inputManagerInstanceField = typeof(CVRInputManager).GetField(nameof(CVRInputManager.Instance));
            FieldInfo movementVectorField = typeof(CVRInputManager).GetField(nameof(CVRInputManager.movementVector));

            MethodInfo getKeyMethod = typeof(Input).GetMethod(nameof(Input.GetKey), new Type[1] { typeof(KeyCode) });

            FieldInfo interactLeftDownField = typeof(CVRInputManager).GetField(nameof(CVRInputManager.interactLeftDown));
            FieldInfo interactRightDownField = typeof(CVRInputManager).GetField(nameof(CVRInputManager.interactRightDown));

            for (int i = 1; i < instrucList.Count; i++)
            {
                CodeInstruction instruction = instrucList[i];

                if (lastInstruction.LoadsField(inputManagerInstanceField) && instruction.LoadsField(movementVectorField, true))
                {
                    // Remove the last instruction added since we need to check
                    // a previous instruction before we're sure that we need to
                    // modify the code
                    result.RemoveAt(i - 1);

                    // Grab labels that we'll need later
                    Label? beforeCanMove = null;

                    Label beforeLeftTrigger = generator.DefineLabel();

                    Label afterIf = (Label)instrucList[i - 2].operand;

                    // Skip any opcodes we will replace
                    i++;
                    do
                    {
                        i++;

                        // Grab a label that we'll need later
                        if (instrucList[i].opcode == OpCodes.Bgt_S || instrucList[i].opcode == OpCodes.Bgt)
                            beforeCanMove = (Label)instrucList[i].operand;
                    }
                    while (!(instrucList[i].opcode == OpCodes.Ble_Un_S || instrucList[i].opcode == OpCodes.Ble_Un));

                    // Inject my il codes

                    // KeyCode.E
                    result.Add(new CodeInstruction(OpCodes.Ldc_I4, (int)KeyCode.E));

                    // Input.GetKey
                    result.Add(new CodeInstruction(OpCodes.Call, getKeyMethod));

                    // Skip to trigger check if false
                    result.Add(new CodeInstruction(OpCodes.Brfalse, beforeLeftTrigger));

                    // KeyCode.Q
                    result.Add(new CodeInstruction(OpCodes.Ldc_I4, (int)KeyCode.Q));

                    // Input.GetKey
                    result.Add(new CodeInstruction(OpCodes.Call, getKeyMethod));

                    // Skip to before canMove check if true
                    result.Add(new CodeInstruction(OpCodes.Brtrue, beforeCanMove.Value));

                    // CVRInputManager.Instance
                    CodeInstruction ldsField = new CodeInstruction(OpCodes.Ldsfld, inputManagerInstanceField);
                    // Do this to mark the labels location
                    ldsField.labels.Add(beforeLeftTrigger);
                    result.Add(ldsField);
                    
                    // CVRInputManager.interactLeftDown
                    result.Add(new CodeInstruction(OpCodes.Ldfld, interactLeftDownField));

                    // Skip to end of if statement if false
                    result.Add(new CodeInstruction(OpCodes.Brfalse, afterIf));

                    // CVRInputManager.Instance
                    result.Add(new CodeInstruction(OpCodes.Ldsfld, inputManagerInstanceField));

                    // CVRInputManager.interactRightDown
                    result.Add(new CodeInstruction(OpCodes.Ldfld, interactRightDownField));

                    // Skip to end of if statement if false
                    result.Add(new CodeInstruction(OpCodes.Brfalse, afterIf));
                } 
                else
                {
                    result.Add(instruction);
                }

                lastInstruction = instruction;
            }

            return result;
        }
    }
}
