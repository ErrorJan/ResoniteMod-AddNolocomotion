using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Elements.Core;
using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine;
using FrooxEngine.UIX;

namespace AddNolocomotion;

public class AddNolocomotion : ResoniteMod 
{
    internal const string VERSION_CONSTANT = "1.0.0"; 
    public override string Name => "Add Nolocomotion";
    public override string Author => "ErrorJan";
    public override string Version => VERSION_CONSTANT;
    public override string Link => "https://github.com/ErrorJan/ResoniteMod-AddNolocomotion";
    private static ModConfiguration? rmlConfig;
    private static List<MethodInfo> foundMethods = new();

    public override void OnEngineInit() 
    {
        rmlConfig = GetConfiguration();
        rmlConfig?.Save( true );
        
        Harmony harmony = new Harmony("ErrorJan.AddNolocomotion");
        
        // ----
        // we do be making crimes against binary
        // ----
        
        PatchProcessor p1 = harmony.CreateProcessor( AccessTools.Method( typeof( InteractionHandler ), "OpenContextMenu" ) );
        p1.AddTranspiler( SymbolExtensions.GetMethodInfo( () => FindAnonymousClassCreator_Transpiler ) );
        p1.Patch(); // not really patch, but find some methods calling some anonymous functions :3
        
        CryIfNeeded();
        
        List<PatchProcessor> p2 = foundMethods.ConvertAll<PatchProcessor>( method => harmony.CreateProcessor( method ) );
        foundMethods.Clear();
        p2.ForEach( processor => processor.AddTranspiler( SymbolExtensions.GetMethodInfo( () => FindAnonymousClass_Transpiler ) ) );
        p2.ForEach( processor => processor.Patch() ); // try finding the class that holds the actual function we want now
        
        CryIfNeeded();
        
        List<PatchProcessor> p3 = foundMethods.ConvertAll<PatchProcessor>( method => harmony.CreateProcessor( method ) );
        foundMethods.Clear();
        p3.ForEach( processor => processor.AddTranspiler( SymbolExtensions.GetMethodInfo( () => AddNoLocomotion_Transpiler ) ) );
        p3.ForEach( processor => processor.Patch() ); // Actually patch what we need
    }

    private static void CryIfNeeded()
    {
        if ( foundMethods.Count == 0 )
            Error( "No anonymous methods found!" );
        else
            Msg( "Found " + foundMethods.Count + " methods! :3" );
    }

    [AutoRegisterConfigKey]
    private static readonly 
        ModConfigurationKey<bool> modEnabled = 
            new("Enabled", 
                "Should the mod be enabled?", 
                () => true);

    // I tried a simpler method, but it seemed to not work...
    // is there a way to make it work?
    // private static void PostFix( object options, float? speedOverride, InteractionHandler __instance ) 
    // {
    //     if ( (int)options == 1 && !__instance.IsUserspaceLaserActive )
    //     {
    //         AddRadialMenuEntryNoLocomotion();
    //     }
    // }

    private static IEnumerable<CodeInstruction> FindAnonymousClassCreator_Transpiler( IEnumerable<CodeInstruction> instructions )
    {
        foreach ( var instruction in instructions ) 
        {
            if ( instruction.opcode == OpCodes.Ldftn ) // Looks like we're calling anonymous stuff
            {
                MethodInfo? method = instruction.operand as MethodInfo;
                if ( method == null || foundMethods.Contains( method ) ) goto soft_continue;
                foundMethods.Add( method );
            }
            
            soft_continue:
            yield return instruction;
        }
    }
    
    private static IEnumerable<CodeInstruction> FindAnonymousClass_Transpiler( IEnumerable<CodeInstruction> instructions )
    {
        foreach ( var instruction in instructions ) 
        {
            if ( instruction.opcode == OpCodes.Ldloca_S ) // as long as the .NET implementation doesn't change, we gucci :3
            {
                LocalVariableInfo? whatthefuckamidoing = instruction.operand as LocalVariableInfo;
                if ( whatthefuckamidoing == null ) goto soft_continue;
    
                // > This type is intended for compiler use only.
                // I mean... We sort of are a """compiler""" and I really just need the name of it :3
                MethodInfo method = AccessTools.Method( whatthefuckamidoing.LocalType, nameof( IAsyncStateMachine.MoveNext ) );
                if ( foundMethods.Contains( method ) ) goto soft_continue;
                foundMethods.Add( method );
            }
            
            soft_continue: // Why the use of goto? So my stupid ass doesn't forget to actually return an instruction
            yield return instruction;
        }
    }
    
    enum SearchStage 
    {
        Start,
        ExpectingCallVirt,
        ExpectingStoreEnumerator,
        GuardStoredEnumeratorAndWaitForMoveNext,
        FoundPatchPoint,
        Patched
    }
    
    private static IEnumerable<CodeInstruction> AddNoLocomotion_Transpiler( IEnumerable<CodeInstruction> instructions )
    {
        SearchStage stage = SearchStage.Start;
        LocalBuilder? localEnumerator = null;
        
        foreach ( var instruction in instructions )
        {
            switch ( stage )
            {
                case SearchStage.Start:
                    if ( instruction.opcode == OpCodes.Ldfld )
                    {
                        FieldInfo? field = instruction.operand as FieldInfo;
                        if ( field == null ) 
                            goto reset;
                        
                        /* 23.11.2025
                         * field.FieldType = SyncRefList<ILocomotionModule>
                         * field.ReflectedType = LocomotionController
                         * field.Name = LocomotionModules
                         */
                        
                        if ( 
                            field.ReflectedType != typeof( LocomotionController ) || 
                            field.FieldType != typeof( SyncRefList<ILocomotionModule> ) 
                        ) goto reset;
                        
                        stage = SearchStage.ExpectingCallVirt;
                        Msg( "stage 1 pass" );
                        break;
                    }
    
                    goto reset;
                
                case SearchStage.ExpectingCallVirt:
                    if ( instruction.opcode == OpCodes.Callvirt )
                    {
                        MethodInfo? method = instruction.operand as MethodInfo;
                        if ( method == null ) 
                            goto reset;
                        if ( method.ReturnType != typeof( SyncRefList<ILocomotionModule>.Enumerator ) ) 
                            goto reset;
                        
                        stage = SearchStage.ExpectingStoreEnumerator;
                        Msg( "stage 2 pass" );
                        break;
                    }
                    
                    goto reset;
                    
                case SearchStage.ExpectingStoreEnumerator:
                    if ( instruction.opcode == OpCodes.Stloc_S )
                    {
                        LocalBuilder? local = instruction.operand as LocalBuilder;
                        if ( local == null ) 
                            goto reset;
                        localEnumerator = local;
                        
                        stage = SearchStage.GuardStoredEnumeratorAndWaitForMoveNext;
                        Msg( "stage 3 pass" );
                        break;
                    }
                    
                    goto reset;
                    
                case SearchStage.GuardStoredEnumeratorAndWaitForMoveNext:
                    if ( instruction.opcode == OpCodes.Stloc_S )
                    {
                        LocalBuilder? local = instruction.operand as LocalBuilder;
                        if ( local != null && local.LocalIndex == localEnumerator?.LocalIndex ) 
                            goto reset;
                    }
                    if ( instruction.opcode == OpCodes.Call )
                    {
                        MethodInfo? method = instruction.operand as MethodInfo;
                        if ( method == null ) 
                            break;
                        if ( method.Name != nameof( SyncRefList<ILocomotionModule>.Enumerator.MoveNext ) )
                            break;
                        
                        stage = SearchStage.FoundPatchPoint;
                        Msg( "stage 4 pass" );
                    }

                    break;
                
                case SearchStage.FoundPatchPoint:
                    if ( instruction.opcode == OpCodes.Brtrue )
                    {
                        yield return instruction;
                        yield return new CodeInstruction( OpCodes.Call, SymbolExtensions.GetMethodInfo( () => AddRadialMenuEntryNoLocomotion() ) );
                        stage = SearchStage.Patched;
                        Msg( "Patched!" );
                        continue;
                    }

                    break;
                
                case SearchStage.Patched:
                    break;
                    
                default: 
                    reset:
                    if ( stage > 0 )
                    {
                        Msg( "reset" );
                        Msg( instruction.ToString() );
                    }
                    stage = SearchStage.Start;
                    break;
            }
            
            yield return instruction;
        }
    }

    public static void AddRadialMenuEntryNoLocomotion()
    {
        if ( !( rmlConfig?.GetValue(modEnabled) ?? true ) ) 
            return;
        
        User localUser = Userspace.Current.Engine.WorldManager.FocusedWorld.LocalUser;
        LocomotionController? controller = localUser.Root?.GetRegisteredComponent<LocomotionController>();
        ContextMenu? ctxMenu = localUser.GetUserContextMenu();
        if ( controller == null || ctxMenu == null ) return;
        
        bool isCurrentlyNocomotion = controller.ActiveModule == null!;
        
        string nolocomotionName = localUser.GetLocalized("Interaction.Locomotion.None");
        if ( isCurrentlyNocomotion )
            nolocomotionName = $"<b>{nolocomotionName}</b>";
        colorX btnColor = MathX.Lerp( colorX.Black, colorX.White, 0.3f );
        
        ContextMenuItem contextMenuItem = ctxMenu.AddItem( nolocomotionName, ( IAssetProvider<ITexture2D >) null!, btnColor );
        contextMenuItem.Highlight.Value = isCurrentlyNocomotion;
        contextMenuItem.Button.SetupLocalAction( SetNoLocomotion );
    }
    
    [SyncMethod( typeof ( Delegate ), null )]
    public static void SetNoLocomotion( IButton button, ButtonEventData eventData )
    {
        User localUser = Userspace.Current.Engine.WorldManager.FocusedWorld.LocalUser;
        LocomotionController controller = localUser.Root.GetRegisteredComponent<LocomotionController>();
        controller.ActiveModule = null!;
        localUser.GetUserContextMenu().Close();
    }
}
