using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace BetterRaids
{
    internal static class ImplantCatalogLogger
    {
        public const string GlobalFactionScopeName = "Global";

        private static readonly List<ImplantPoolRule> VanillaRules = new List<ImplantPoolRule>
        {
            // Low-tech replacements. "Tribal" mods usually map this to RimWorld's Neolithic tech level.
            Rule("PegLeg", ImplantTechTier.Animal, ImplantUsefulness.CoreCombat),
            Rule("PegLeg", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            Rule("PegLeg", ImplantTechTier.Medieval, ImplantUsefulness.CoreCombat),
            Rule("WoodenHand", ImplantTechTier.Animal, ImplantUsefulness.CoreCombat),
            Rule("WoodenHand", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            Rule("WoodenHand", ImplantTechTier.Medieval, ImplantUsefulness.CoreCombat),
            Rule("WoodenFoot", ImplantTechTier.Animal, ImplantUsefulness.CoreCombat),
            Rule("WoodenFoot", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            Rule("WoodenFoot", ImplantTechTier.Medieval, ImplantUsefulness.CoreCombat),
            Rule("Denture", ImplantTechTier.Neolithic, ImplantUsefulness.LowCombat),
            Rule("Denture", ImplantTechTier.Medieval, ImplantUsefulness.LowCombat),

            // Industrial prosthetics. Worse than natural/bionic, but believable for industrial factions.
            Rule("SimpleProstheticLeg", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            Rule("SimpleProstheticArm", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            Rule("SimpleProstheticHeart", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            Rule("CochlearImplant", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),

            // Spacer combat bionics and common utility implants.
            Rule("BionicEye", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            Rule("BionicArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            Rule("BionicLeg", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            Rule("BionicSpine", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            Rule("BionicHeart", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            Rule("BionicStomach", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            Rule("BionicEar", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            Rule("BionicTongue", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            Rule("BionicJaw", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            Rule("PowerClaw", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            Rule("Painstopper", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            Rule("Joywire", ImplantTechTier.Spacer, ImplantUsefulness.Special),
            Rule("DeathAcidifier", ImplantTechTier.Spacer, ImplantUsefulness.Special),

            // Ultra is intentionally below archotech. Core/WTL has no normal Ultra body-part replacements yet.

            // Archotech is its own post-Ultra pool.
            Rule("ArchotechEye", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            Rule("ArchotechArm", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            Rule("ArchotechLeg", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat)
        };

        private static readonly List<ImplantPoolRule> ModPatchRules = new List<ImplantPoolRule>
        {
            // [sbz] Archotech Brain
            ModRule("jgh.archotechbrain", "ProstheticBrain", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("jgh.archotechbrain", "BionicBrain", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("jgh.archotechbrain", "ArchotechBrain", ImplantTechTier.Archotech, ImplantUsefulness.SupportCombat),

            // Anima Bionics: nature/tribal replacements, intentionally not industrial cybernetics.
            ModRule("seti.victor.notdemo.animabodies", "AnimaEar", ImplantTechTier.Neolithic, ImplantUsefulness.SupportCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaEntArm", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaEye", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaHeart", ImplantTechTier.Neolithic, ImplantUsefulness.SupportCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaHorn", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaJaw", ImplantTechTier.Neolithic, ImplantUsefulness.LowCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaKidney", ImplantTechTier.Neolithic, ImplantUsefulness.SupportCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaLeg", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaLiver", ImplantTechTier.Neolithic, ImplantUsefulness.SupportCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaLung", ImplantTechTier.Neolithic, ImplantUsefulness.SupportCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaNose", ImplantTechTier.Neolithic, ImplantUsefulness.LowCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaPlate", ImplantTechTier.Neolithic, ImplantUsefulness.SupportCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaSpine", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaStomach", ImplantTechTier.Neolithic, ImplantUsefulness.SupportCombat),
            ModRule("seti.victor.notdemo.animabodies", "AnimaWarArm", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            ModRule("seti.victor.notdemo.animabodies", "MindDryad", ImplantTechTier.Neolithic, ImplantUsefulness.Special),
            ModRule("seti.victor.notdemo.animabodies", "PsychicDryad", ImplantTechTier.Neolithic, ImplantUsefulness.Special),

            // Visible Cybernetics
            ModRule("ghastly.visualcybernetics", "Gha_BuilderAppendage", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("ghastly.visualcybernetics", "Gha_CrafterAppendage", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("ghastly.visualcybernetics", "Gha_DrillAppendage", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("ghastly.visualcybernetics", "Gha_ReconEye", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),

            // The Dead Man's Switch
            FactionModRule("aoba.deadmanswitch.core", "DMS_NutrientPort", ImplantTechTier.Industrial, ImplantUsefulness.LowCombat, "DMS_Army"),
            FactionModRule("aoba.deadmanswitch.core", "DMS_ProstheticArm", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat, "DMS_Army"),
            FactionModRule("aoba.deadmanswitch.core", "DMS_ProstheticEye", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat, "DMS_Army"),
            FactionModRule("aoba.deadmanswitch.core", "DMS_ProstheticKidney", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat, "DMS_Army"),
            FactionModRule("aoba.deadmanswitch.core", "DMS_ProstheticLeg", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat, "DMS_Army"),
            FactionModRule("aoba.deadmanswitch.core", "DMS_ProstheticLung", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat, "DMS_Army"),
            FactionModRule("aoba.deadmanswitch.core", "DMS_ProstheticSpine", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat, "DMS_Army"),
            FactionModRule("aoba.deadmanswitch.core", "DMS_SensoryFilter", ImplantTechTier.Industrial, ImplantUsefulness.Special, "DMS_Army"),
            FactionModRule("aoba.deadmanswitch.core", "DMS_SyntheticArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat, "DMS_Army"),
            FactionModRule("aoba.deadmanswitch.core", "DMS_SyntheticLeg", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat, "DMS_Army"),

            // Altered Carbon 2: ReSleeved
            ModRule("hlx.ultratechalteredcarbon", "AC_NeuralStack", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("hlx.ultratechalteredcarbon", "AC_RemoteStack", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("hlx.ultratechalteredcarbon", "AC_Dreamcatcher", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("hlx.ultratechalteredcarbon", "AC_MentalFuse", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("hlx.ultratechalteredcarbon", "AC_CortexOverseer", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("hlx.ultratechalteredcarbon", "AC_VoiceSynthesizer", ImplantTechTier.Ultra, ImplantUsefulness.LowCombat),
            ModRule("hlx.ultratechalteredcarbon", "AC_RogianArmBlade", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("hlx.ultratechalteredcarbon", "AC_RogianArmBlade_Mono", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("hlx.ultratechalteredcarbon", "AC_RogianArmBlade_Plasma", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("hlx.ultratechalteredcarbon", "AC_RogianArmBlade_Toxblade", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("hlx.ultratechalteredcarbon", "AC_RogianArmBlade_Zeus", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("hlx.ultratechalteredcarbon", "AC_ArchoStack", ImplantTechTier.Archotech, ImplantUsefulness.Special),
            ModRule("hlx.ultratechalteredcarbon", "AC_ArchotechStack", ImplantTechTier.Archotech, ImplantUsefulness.Special),
            ModRule("hlx.ultratechalteredcarbon", "VFEU_CorticalStack", ImplantTechTier.Ultra, ImplantUsefulness.Special),

            // Ushankas Glittertech Expansion. Glittertech body parts are explicit Ultra tech, not Archotech.
            ModRule("ushanka.glittertechexpansion", "USH_InstalledCryogenicNexus", ImplantTechTier.Ultra, ImplantUsefulness.SupportCombat),
            ModRule("ushanka.glittertechexpansion", "USH_InstalledGlitterlink", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("ushanka.glittertechexpansion", "USH_InstalledGoldenSkinReplacement", ImplantTechTier.Ultra, ImplantUsefulness.SupportCombat),
            ModRule("ushanka.glittertechexpansion", "USH_InstalledGoldenTeethReplacement", ImplantTechTier.Ultra, ImplantUsefulness.LowCombat),
            ModRule("ushanka.glittertechexpansion", "USH_InstalledMemoryProjector", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("ushanka.glittertechexpansion", "USH_InstalledPlasteelSkinReplacement", ImplantTechTier.Ultra, ImplantUsefulness.SupportCombat),
            ModRule("ushanka.glittertechexpansion", "USH_InstalledPlasteelTeethReplacement", ImplantTechTier.Ultra, ImplantUsefulness.LowCombat),
            ModRule("ushanka.glittertechexpansion", "USH_InstalledTelepadIntegrator", ImplantTechTier.Ultra, ImplantUsefulness.Special),

            // Expanded Prosthetics and Organ Engineering - Forked
            ModRule("vat.epoeforked", "EyePatch", ImplantTechTier.Medieval, ImplantUsefulness.LowCombat),
            ModRule("vat.epoeforked", "EarBandage", ImplantTechTier.Medieval, ImplantUsefulness.LowCombat),
            ModRule("vat.epoeforked", "BasicWoodenFinger", ImplantTechTier.Medieval, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "BasicWoodenToe", ImplantTechTier.Medieval, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "HookHand", ImplantTechTier.Medieval, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "SteelArm", ImplantTechTier.Medieval, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "GoldenEye", ImplantTechTier.Medieval, ImplantUsefulness.LowCombat),
            ModRule("vat.epoeforked", "SimpleProstheticFinger", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "SimpleProstheticFoot", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "SimpleProstheticHand", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "SimpleProstheticToe", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "ReplacementRadius", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "LightReceptor", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "ArtificialNose", ImplantTechTier.Industrial, ImplantUsefulness.LowCombat),
            ModRule("vat.epoeforked", "SimpleSpine", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "SurrogateKidney", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "SurrogateLiver", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "SurrogateLung", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "SurrogateStomach", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "AIChip", ImplantTechTier.Industrial, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "BrainStimulator", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "ConstructorCore", ImplantTechTier.Industrial, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "DiplomatCore", ImplantTechTier.Industrial, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "DoctorCore", ImplantTechTier.Industrial, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "FarmerCore", ImplantTechTier.Industrial, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "MinerCore", ImplantTechTier.Industrial, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "EPIA_AuxiliaryAI_Artisan", ImplantTechTier.Industrial, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "EPIA_AuxiliaryAI_Brawler", ImplantTechTier.Industrial, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "EPIA_AuxiliaryAI_Commando", ImplantTechTier.Industrial, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "EPIA_AuxiliaryAI_Sharpshooter", ImplantTechTier.Industrial, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "BionicFinger", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "BionicFoot", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "BionicHand", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "BionicHeart", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "BionicJaw", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            ModRule("vat.epoeforked", "BionicStomach", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "BionicToe", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "SyntheticKidney", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "SyntheticLiver", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "SyntheticLung", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "PowerArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "HydraulicJaw", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            ModRule("vat.epoeforked", "SilentJaw", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            ModRule("vat.epoeforked", "TacticalCorneaImplant", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "EPIA_TacticalBionicEye", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "EPOE_ScytherBlade", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "MuscleStimulatorArms", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "MuscleStimulatorLegs", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "AdrenalineRib", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "CoagulatorRib", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "CoolerRib", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "DruggedRib", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "HeaterRib", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "MedicalRib", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "PainkillerRib", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "RespirationRib", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "WakeUpRib", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "EPOE_InstinctOptimizedEyes", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "EPOE_InstinctOptimizedFoot", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "EPOE_InstinctOptimizedHand", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "EPOE_OrganicOptimized", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "AdvancedBionicArm", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "AdvancedBionicEar", ImplantTechTier.Ultra, ImplantUsefulness.SupportCombat),
            ModRule("vat.epoeforked", "AdvancedBionicEye", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "AdvancedBionicFinger", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "AdvancedBionicFoot", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "AdvancedBionicHand", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "AdvancedBionicJaw", ImplantTechTier.Ultra, ImplantUsefulness.LowCombat),
            ModRule("vat.epoeforked", "AdvancedBionicLeg", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "AdvancedBionicSpine", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "AdvancedBionicToe", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "AdvancedPowerArm", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "AIPersonaCore", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("vat.epoeforked", "ExoskeletonSuit", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("vat.epoeforked", "EPIA_ProtectiveExoskeleton", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),

            // Integrated Implants - selected explicit body replacements and combat implants.
            ModRule("lts.i", "LTS_BionicKidney", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_BionicLiver", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_BionicLung", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_BionicNose", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            ModRule("lts.i", "LTS_BionicTail", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ModularBionicArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ModularBionicEye", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ModularBionicJaw", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            ModRule("lts.i", "LTS_ModularBionicKidney", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_ModularBionicLeg", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ModularBionicLung", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_ModularBionicNose", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            ModRule("lts.i", "LTS_ModularBionicSpine", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ModularBionicStomach", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LeftExtraBionicArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraBionicArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LeftExtraPowerArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraPowerArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LeftExtraPowerClaw", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraPowerClaw", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LeftExtraDrillArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraDrillArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LeftExtraFieldHand", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraFieldHand", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LeftExtraNerveShredderClaw", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraNerveShredderClaw", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "DeadlifeVenomClaw", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_SubdermalArmour", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "SkeletalBracing", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "StrengthEnhancer", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "WiredReflex", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_CommsImplant", ImplantTechTier.Spacer, ImplantUsefulness.Special),
            ModRule("lts.i", "LTS_GunneryAssistant", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LeftExtraAdvancedBionicArm", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraAdvancedBionicArm", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LeftExtraAdvancedPowerArm", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraAdvancedPowerArm", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LeftExtraAdvancedDrillArm", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraAdvancedDrillArm", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LeftExtraAdvancedFieldHand", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraAdvancedFieldHand", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ShieldImplant", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("lts.i", "LTS_EmergencyShield", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("lts.i", "LTS_StealthSystem", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("lts.i", "LTS_Gravlifter", ImplantTechTier.Ultra, ImplantUsefulness.Special),
            ModRule("lts.i", "LTS_ShoulderTurret", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ShoulderBeamTurret", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ShoulderChargeTurret", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ShoulderMortar", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ShoulderRocketPod", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ArchotechBlackbox", ImplantTechTier.Archotech, ImplantUsefulness.Special),
            ModRule("lts.i", "ArchotechVoicebox", ImplantTechTier.Archotech, ImplantUsefulness.LowCombat),
            ModRule("lts.i", "Archowomb", ImplantTechTier.Archotech, ImplantUsefulness.Special),
            ModRule("lts.i", "LTS_ArchotechEar", ImplantTechTier.Archotech, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_ArchotechHeart", ImplantTechTier.Archotech, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_ArchotechJaw", ImplantTechTier.Archotech, ImplantUsefulness.LowCombat),
            ModRule("lts.i", "LTS_ArchotechKidney", ImplantTechTier.Archotech, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_ArchotechLiver", ImplantTechTier.Archotech, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_ArchotechLung", ImplantTechTier.Archotech, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_ArchotechNose", ImplantTechTier.Archotech, ImplantUsefulness.LowCombat),
            ModRule("lts.i", "LTS_ArchotechSpine", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ArchotechStomach", ImplantTechTier.Archotech, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_ModularArchotechArm", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ModularArchotechEye", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ModularArchotechJaw", ImplantTechTier.Archotech, ImplantUsefulness.LowCombat),
            ModRule("lts.i", "LTS_ModularArchotechKidney", ImplantTechTier.Archotech, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_ModularArchotechLeg", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ModularArchotechLung", ImplantTechTier.Archotech, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LTS_ModularArchotechNose", ImplantTechTier.Archotech, ImplantUsefulness.LowCombat),
            ModRule("lts.i", "LTS_ModularArchotechSpine", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "LTS_ModularArchotechStomach", ImplantTechTier.Archotech, ImplantUsefulness.SupportCombat),
            ModRule("lts.i", "LeftExtraArchotechArm", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            ModRule("lts.i", "RightExtraArchotechArm", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),

            // Integrated Implants - EPOE Modular Compatibility
            ModRule("asunib.epoeiicompat", "LTS_EPOE_ModularAdvancedBionicArm", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("asunib.epoeiicompat", "LTS_EPOE_ModularAdvancedBionicEye", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("asunib.epoeiicompat", "LTS_EPOE_ModularAdvancedBionicJaw", ImplantTechTier.Ultra, ImplantUsefulness.LowCombat),
            ModRule("asunib.epoeiicompat", "LTS_EPOE_ModularAdvancedBionicLeg", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("asunib.epoeiicompat", "LTS_EPOE_ModularAdvancedBionicSpine", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),
            ModRule("asunib.epoeiicompat", "LTS_EPOE_ModularAdvancedPowerArm", ImplantTechTier.Ultra, ImplantUsefulness.CoreCombat),

            // Alpha Implants: animal-specific replacements. These are explicit, not auto-classified.
            ModRule("sarg.alphaimplants", "AI_WoodenLimb", ImplantTechTier.Animal, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_ClothTail", ImplantTechTier.Animal, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalDenture", ImplantTechTier.Animal, ImplantUsefulness.LowCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticArm", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticBeak", ImplantTechTier.Industrial, ImplantUsefulness.LowCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticBlade", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticHeart", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticJaw", ImplantTechTier.Industrial, ImplantUsefulness.LowCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticKidney", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticLeg", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticLiver", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticLung", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticSpine", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalProstheticStomach", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicBeak", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicBlade", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicEar", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicEye", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicHeart", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicJaw", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicKidney", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicLeg", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicLiver", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicLung", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicNose", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicSpine", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalBionicStomach", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalPowerClaw", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalPainstopper", ImplantTechTier.Spacer, ImplantUsefulness.Special),
            ModRule("sarg.alphaimplants", "AI_AnimalArchotechArm", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalArchotechLeg", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalArchotechTail", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            ModRule("sarg.alphaimplants", "AI_AnimalSynapticReinforcer", ImplantTechTier.Archotech, ImplantUsefulness.Special)
        };

        public static void LogCatalog()
        {
            try
            {
                Verse.Log.Message(BuildCatalogText(true));
            }
            catch (Exception exception)
            {
                Verse.Log.Warning("[BetterRaids] Failed to build implant tech pools: " + exception);
            }
        }

        public static string BuildSettingsCatalogText()
        {
            try
            {
                return BuildCatalogText(false);
            }
            catch (Exception exception)
            {
                return "[BetterRaids] Failed to build implant tech pools: " + exception;
            }
        }

        internal static List<string> GetTechTierNames()
        {
            return Enum.GetNames(typeof(ImplantTechTier)).ToList();
        }

        internal static List<string> GetFactionScopeNames()
        {
            List<string> scopes = new List<string> { GlobalFactionScopeName };
            scopes.AddRange(DefDatabase<FactionDef>.AllDefs
                .Where(def => def != null && !string.IsNullOrEmpty(def.defName))
                .Select(def => def.defName));
            scopes.AddRange(GetLoadedModPatchRules()
                .SelectMany(rule => rule.AllowedFactionDefNames)
                .Where(scope => !string.IsNullOrEmpty(scope))
                .ToList());

            return scopes
                .Distinct()
                .OrderBy(scope => scope == GlobalFactionScopeName ? string.Empty : scope)
                .ToList();
        }

        internal static List<ImplantTierSummary> GetTierSummaries(string factionScopeName)
        {
            List<ImplantPoolEntry> entries = FilterEntriesForFaction(BuildResolvedEntries(), factionScopeName);
            List<ImplantTierSummary> summaries = new List<ImplantTierSummary>();

            foreach (ImplantTechTier tier in Enum.GetValues(typeof(ImplantTechTier)))
            {
                summaries.Add(new ImplantTierSummary(tier.ToString(), entries.Count(entry => entry.TechTier == tier)));
            }

            return summaries;
        }

        internal static List<string> GetBodyPartNames(string techTierName, bool includeFallback, string factionScopeName)
        {
            ImplantTechTier tier;
            if (!TryParseTier(techTierName, out tier))
            {
                return new List<string>();
            }

            List<ImplantPoolEntry> entries = FilterEntriesForFaction(BuildResolvedEntries(), factionScopeName);
            List<string> bodyParts = entries
                .Select(entry => entry.BodyPartDefName)
                .Where(part => !string.IsNullOrEmpty(part) && part != "unknown")
                .Distinct()
                .OrderBy(part => part)
                .ToList();

            if (!includeFallback)
            {
                return bodyParts
                    .Where(part => entries.Any(entry => entry.TechTier == tier && entry.BodyPartDefName == part))
                    .ToList();
            }

            return bodyParts
                .Where(part => GetEffectivePool(entries, tier, part).Entries.Count > 0)
                .ToList();
        }

        internal static ImplantPoolSelection GetSelectedPool(string techTierName, string bodyPartDefName, bool includeFallback, string factionScopeName)
        {
            ImplantTechTier tier;
            if (!TryParseTier(techTierName, out tier) || string.IsNullOrEmpty(bodyPartDefName))
            {
                return ImplantPoolSelection.Empty(techTierName, bodyPartDefName);
            }

            List<ImplantPoolEntry> entries = FilterEntriesForFaction(BuildResolvedEntries(), factionScopeName);
            EffectivePool pool = includeFallback
                ? GetEffectivePool(entries, tier, bodyPartDefName)
                : new EffectivePool(tier, entries
                    .Where(entry => entry.TechTier == tier && entry.BodyPartDefName == bodyPartDefName)
                    .OrderBy(entry => entry.Usefulness)
                    .ThenBy(entry => entry.DefName)
                    .ToList());

            return new ImplantPoolSelection(
                tier.ToString(),
                bodyPartDefName,
                pool.SourceTier.ToString(),
                pool.SourceTier != tier,
                pool.Entries.Select(ToViewEntry).ToList());
        }

        internal static List<ImplantUpgradeCandidate> GetEliteUpgradeCandidates(string techTierName, string factionScopeName)
        {
            ImplantTechTier tier;
            if (!TryParseTier(techTierName, out tier))
            {
                return new List<ImplantUpgradeCandidate>();
            }

            List<ImplantPoolEntry> entries = FilterEntriesForFaction(BuildResolvedEntries(), factionScopeName);
            List<string> bodyParts = entries
                .Select(entry => entry.BodyPartDefName)
                .Where(part => !string.IsNullOrEmpty(part) && part != "unknown")
                .Distinct()
                .ToList();

            Dictionary<string, ImplantUpgradeCandidate> candidatesByDefName = new Dictionary<string, ImplantUpgradeCandidate>();
            for (int i = 0; i < bodyParts.Count; i++)
            {
                EffectivePool pool = GetEffectivePool(entries, tier, bodyParts[i]);
                for (int j = 0; j < pool.Entries.Count; j++)
                {
                    ImplantPoolEntry entry = pool.Entries[j];
                    if (entry.HediffDef == null || entry.Usefulness == ImplantUsefulness.Special)
                    {
                        continue;
                    }

                    if (!candidatesByDefName.ContainsKey(entry.DefName))
                    {
                        candidatesByDefName.Add(entry.DefName, new ImplantUpgradeCandidate(
                            entry.HediffDef,
                            entry.DefName,
                            entry.Label,
                            entry.BodyPartDefName,
                            entry.Usefulness.ToString(),
                            EstimateRaidPointCost(entry)));
                    }
                }
            }

            return candidatesByDefName.Values
                .OrderByDescending(candidate => candidate.EstimatedRaidPointCost)
                .ThenBy(candidate => candidate.DefName)
                .ToList();
        }

        private static string BuildCatalogText(bool includeUnmapped)
        {
            List<ImplantPoolEntry> entries = BuildResolvedEntries();

            HashSet<string> mappedDefNames = new HashSet<string>(entries.Select(entry => entry.DefName));
            List<HediffDef> unmapped = DefDatabase<HediffDef>.AllDefs
                .Where(IsImplantOrAddedPart)
                .Where(def => !mappedDefNames.Contains(def.defName))
                .OrderBy(def => GetSourceMod(def))
                .ThenBy(def => def.defName)
                .ToList();

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[BetterRaids] Implant tech pools");
            builder.AppendLine("  tierSource=RimWorld TechLevel enum + World Tech Level/VFE naming");
            builder.AppendLine("  note=Neolithic is the vanilla enum value used for tribal tech");
            builder.AppendLine("  supportedTierMods:");
            builder.AppendLine("    WorldTechLevel=" + IsModLoaded("m00nl1ght.WorldTechLevel"));
            builder.AppendLine("    ProgressionCore=" + IsModLoaded("ferny.progressioncore"));
            builder.AppendLine("    VFETribals=" + IsModLoaded("oskarpotocki.vfe.tribals"));
            builder.AppendLine("  fallbackRule=same body part only, max previous tech tiers=" + GetMaxFallbackTechTierDrop());
            builder.AppendLine("  manualRules=" + VanillaRules.Count);
            builder.AppendLine("  modPatchRules=" + ModPatchRules.Count);
            builder.AppendLine("  loadedModPatchRules=" + GetLoadedModPatchRules().Count);
            builder.AppendLine("  resolvedRules=" + entries.Count);
            builder.AppendLine("  unmappedImplants=" + unmapped.Count);
            AppendTierSummary(builder, entries);
            AppendDirectPoolMatrix(builder, entries);
            AppendEffectivePoolMatrix(builder, entries);

            if (includeUnmapped)
            {
                AppendUnmappedSummary(builder, unmapped);
            }
            else
            {
                builder.AppendLine("  unmapped implants/prosthetics: hidden in Mod Config; press Recalculate to log full list");
            }

            return builder.ToString();
        }

        private static List<ImplantPoolEntry> BuildResolvedEntries()
        {
            List<ImplantPoolRule> rules = new List<ImplantPoolRule>();
            rules.AddRange(VanillaRules);
            rules.AddRange(GetLoadedModPatchRules());

            return rules
                .GroupBy(rule => rule.HediffDefName + "|" + rule.TechTier + "|" + GetFactionScopeKey(rule.AllowedFactionDefNames))
                .Select(group => group.First())
                .Select(BuildEntry)
                .Where(entry => entry.HediffDef != null)
                .OrderBy(entry => entry.TechTier)
                .ThenBy(entry => entry.Usefulness)
                .ThenBy(entry => entry.DefName)
                .ToList();
        }

        private static List<ImplantPoolEntry> FilterEntriesForFaction(List<ImplantPoolEntry> entries, string factionScopeName)
        {
            string normalizedScope = NormalizeFactionScopeName(factionScopeName);
            return entries
                .Where(entry => IsEntryAllowedForFaction(entry, normalizedScope))
                .ToList();
        }

        private static bool IsEntryAllowedForFaction(ImplantPoolEntry entry, string factionScopeName)
        {
            if (entry.AllowedFactionDefNames.Count == 0)
            {
                return true;
            }

            if (factionScopeName == GlobalFactionScopeName)
            {
                return false;
            }

            return entry.AllowedFactionDefNames.Contains(factionScopeName);
        }

        private static string NormalizeFactionScopeName(string factionScopeName)
        {
            return string.IsNullOrEmpty(factionScopeName) ? GlobalFactionScopeName : factionScopeName;
        }

        private static string GetFactionScopeKey(List<string> factionScopeNames)
        {
            if (factionScopeNames == null || factionScopeNames.Count == 0)
            {
                return GlobalFactionScopeName;
            }

            return string.Join(",", factionScopeNames.OrderBy(scope => scope).ToArray());
        }

        private static List<ImplantPoolRule> GetLoadedModPatchRules()
        {
            return ModPatchRules
                .Where(rule => IsModLoaded(rule.PackageId))
                .ToList();
        }

        private static ImplantPoolViewEntry ToViewEntry(ImplantPoolEntry entry)
        {
            return new ImplantPoolViewEntry(
                entry.DefName,
                entry.Label,
                entry.Usefulness.ToString(),
                entry.BodyPartDefName,
                FormatEfficiency(entry.Efficiency),
                entry.SourceMod,
                entry.PackageId,
                FormatFactionScope(entry.AllowedFactionDefNames));
        }

        private static bool TryParseTier(string techTierName, out ImplantTechTier tier)
        {
            if (!string.IsNullOrEmpty(techTierName))
            {
                foreach (ImplantTechTier candidate in Enum.GetValues(typeof(ImplantTechTier)))
                {
                    if (candidate.ToString() == techTierName)
                    {
                        tier = candidate;
                        return true;
                    }
                }
            }

            tier = ImplantTechTier.Spacer;
            return false;
        }

        private static int GetMaxFallbackTechTierDrop()
        {
            BetterRaidsSettings settings = BetterRaidsMod.Settings;
            int value = settings != null ? settings.MaxFallbackTechTierDrop : BetterRaidsSettings.DefaultMaxFallbackTechTierDrop;

            if (value < BetterRaidsSettings.MinFallbackTechTierDrop)
            {
                return BetterRaidsSettings.MinFallbackTechTierDrop;
            }

            if (value > BetterRaidsSettings.MaxFallbackTechTierDropLimit)
            {
                return BetterRaidsSettings.MaxFallbackTechTierDropLimit;
            }

            return value;
        }

        private static ImplantPoolRule Rule(string hediffDefName, ImplantTechTier techTier, ImplantUsefulness usefulness)
        {
            return new ImplantPoolRule(hediffDefName, techTier, usefulness, null, new List<string>());
        }

        private static ImplantPoolRule ModRule(string packageId, string hediffDefName, ImplantTechTier techTier, ImplantUsefulness usefulness)
        {
            return new ImplantPoolRule(hediffDefName, techTier, usefulness, packageId, new List<string>());
        }

        private static ImplantPoolRule FactionModRule(string packageId, string hediffDefName, ImplantTechTier techTier, ImplantUsefulness usefulness, params string[] factionDefNames)
        {
            List<string> scopes = factionDefNames == null
                ? new List<string>()
                : factionDefNames.Where(name => !string.IsNullOrEmpty(name)).Distinct().OrderBy(name => name).ToList();

            return new ImplantPoolRule(hediffDefName, techTier, usefulness, packageId, scopes);
        }

        private static ImplantPoolEntry BuildEntry(ImplantPoolRule rule)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(rule.HediffDefName);
            RecipeDef recipe = def != null ? FindInstallRecipe(def) : null;

            return new ImplantPoolEntry
            {
                HediffDef = def,
                DefName = rule.HediffDefName,
                Label = def != null ? def.label : "missing",
                TechTier = rule.TechTier,
                Usefulness = rule.Usefulness,
                BodyPartDefName = GetRecipeBodyPart(recipe),
                Efficiency = def != null && def.addedPartProps != null ? (float?)def.addedPartProps.partEfficiency : null,
                SourceMod = GetSourceMod(def),
                PackageId = string.IsNullOrEmpty(rule.PackageId) ? "manual" : rule.PackageId,
                AllowedFactionDefNames = new List<string>(rule.AllowedFactionDefNames)
            };
        }

        private static bool IsImplantOrAddedPart(HediffDef def)
        {
            if (def == null)
            {
                return false;
            }

            if (def.addedPartProps != null)
            {
                return true;
            }

            Type hediffClass = def.hediffClass;
            return hediffClass != null && typeof(Hediff_Implant).IsAssignableFrom(hediffClass);
        }

        private static RecipeDef FindInstallRecipe(HediffDef hediffDef)
        {
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefs)
            {
                if (recipe != null && recipe.addsHediff == hediffDef)
                {
                    return recipe;
                }
            }

            return null;
        }

        private static string GetRecipeBodyPart(RecipeDef recipe)
        {
            if (recipe == null || recipe.appliedOnFixedBodyParts == null || recipe.appliedOnFixedBodyParts.Count == 0)
            {
                return "unknown";
            }

            BodyPartDef partDef = recipe.appliedOnFixedBodyParts[0];
            return partDef != null ? partDef.defName : "unknown";
        }

        private static void AppendTierSummary(StringBuilder builder, List<ImplantPoolEntry> entries)
        {
            builder.AppendLine("  tech tiers:");
            foreach (ImplantTechTier tier in Enum.GetValues(typeof(ImplantTechTier)))
            {
                builder.AppendLine("    " + tier + "=" + entries.Count(entry => entry.TechTier == tier));
            }
        }

        private static void AppendDirectPoolMatrix(StringBuilder builder, List<ImplantPoolEntry> entries)
        {
            builder.AppendLine("  direct pools by tech tier and body part:");
            foreach (ImplantTechTier tier in Enum.GetValues(typeof(ImplantTechTier)))
            {
                builder.AppendLine("    " + tier + ":");
                AppendTierBodyPartPools(builder, entries.Where(entry => entry.TechTier == tier).ToList(), "      ");
            }
        }

        private static void AppendEffectivePoolMatrix(StringBuilder builder, List<ImplantPoolEntry> entries)
        {
            List<string> bodyParts = entries
                .Select(entry => entry.BodyPartDefName)
                .Where(part => !string.IsNullOrEmpty(part) && part != "unknown")
                .Distinct()
                .OrderBy(part => part)
                .ToList();

            builder.AppendLine("  effective pools with same-body-part fallback:");
            foreach (ImplantTechTier tier in Enum.GetValues(typeof(ImplantTechTier)))
            {
                builder.AppendLine("    " + tier + ":");
                bool any = false;

                for (int i = 0; i < bodyParts.Count; i++)
                {
                    EffectivePool pool = GetEffectivePool(entries, tier, bodyParts[i]);
                    if (pool.Entries.Count == 0)
                    {
                        continue;
                    }

                    any = true;
                    string fallbackText = pool.SourceTier == tier ? "direct" : "fallback=" + pool.SourceTier;
                    builder.AppendLine("      " + bodyParts[i] + " (" + fallbackText + "): " + FormatEntryList(pool.Entries));
                }

                if (!any)
                {
                    builder.AppendLine("      none");
                }
            }
        }

        private static void AppendTierBodyPartPools(StringBuilder builder, List<ImplantPoolEntry> entries, string indent)
        {
            if (entries.Count == 0)
            {
                builder.AppendLine(indent + "none");
                return;
            }

            foreach (IGrouping<string, ImplantPoolEntry> group in entries.GroupBy(entry => entry.BodyPartDefName).OrderBy(group => group.Key))
            {
                builder.AppendLine(indent + group.Key + ": " + FormatEntryList(group.ToList()));
            }
        }

        private static EffectivePool GetEffectivePool(List<ImplantPoolEntry> entries, ImplantTechTier tier, string bodyPartDefName)
        {
            ImplantTechTier current = tier;
            int fallbackDepth = 0;
            int maxFallbackDepth = GetMaxFallbackTechTierDrop();

            while (true)
            {
                List<ImplantPoolEntry> direct = entries
                    .Where(entry => entry.TechTier == current && entry.BodyPartDefName == bodyPartDefName)
                    .OrderBy(entry => entry.Usefulness)
                    .ThenBy(entry => entry.DefName)
                    .ToList();

                if (direct.Count > 0)
                {
                    return new EffectivePool(current, direct);
                }

                if (fallbackDepth >= maxFallbackDepth)
                {
                    return new EffectivePool(tier, new List<ImplantPoolEntry>());
                }

                if (!TryGetPreviousTier(current, out current))
                {
                    return new EffectivePool(tier, new List<ImplantPoolEntry>());
                }

                fallbackDepth++;
            }
        }

        private static bool TryGetPreviousTier(ImplantTechTier tier, out ImplantTechTier previous)
        {
            switch (tier)
            {
                case ImplantTechTier.Neolithic:
                    previous = ImplantTechTier.Animal;
                    return true;
                case ImplantTechTier.Medieval:
                    previous = ImplantTechTier.Neolithic;
                    return true;
                case ImplantTechTier.Industrial:
                    previous = ImplantTechTier.Medieval;
                    return true;
                case ImplantTechTier.Spacer:
                    previous = ImplantTechTier.Industrial;
                    return true;
                case ImplantTechTier.Ultra:
                    previous = ImplantTechTier.Spacer;
                    return true;
                case ImplantTechTier.Archotech:
                    previous = ImplantTechTier.Ultra;
                    return true;
                default:
                    previous = tier;
                    return false;
            }
        }

        private static string FormatEntryList(List<ImplantPoolEntry> entries)
        {
            return string.Join(", ", entries.Select(FormatEntry).ToArray());
        }

        private static string FormatEntry(ImplantPoolEntry entry)
        {
            return entry.DefName
                + "[" + entry.Usefulness
                + ", eff=" + FormatEfficiency(entry.Efficiency)
                + ", scope=" + FormatFactionScope(entry.AllowedFactionDefNames)
                + ", patch=" + entry.PackageId
                + ", mod=" + entry.SourceMod + "]";
        }

        private static string FormatFactionScope(List<string> factionScopeNames)
        {
            if (factionScopeNames == null || factionScopeNames.Count == 0)
            {
                return GlobalFactionScopeName;
            }

            return string.Join(",", factionScopeNames.ToArray());
        }

        private static void AppendUnmappedSummary(StringBuilder builder, List<HediffDef> unmapped)
        {
            builder.AppendLine("  unmapped implants/prosthetics:");
            foreach (IGrouping<string, HediffDef> group in unmapped.GroupBy(GetSourceMod).OrderBy(group => group.Key))
            {
                builder.AppendLine("    " + group.Key + "=" + group.Count());
                foreach (HediffDef def in group.Take(40))
                {
                    builder.AppendLine("      " + def.defName + " | label=" + def.label + " | efficiency=" + FormatEfficiency(def.addedPartProps != null ? (float?)def.addedPartProps.partEfficiency : null));
                }

                int omitted = group.Count() - 40;
                if (omitted > 0)
                {
                    builder.AppendLine("      ... omitted " + omitted + " more");
                }
            }
        }

        private static string GetSourceMod(Def def)
        {
            if (def == null || def.modContentPack == null)
            {
                return "Core/unknown";
            }

            string packageId = def.modContentPack.PackageId;
            string name = def.modContentPack.Name;

            if (string.IsNullOrEmpty(packageId))
            {
                return string.IsNullOrEmpty(name) ? "Core/unknown" : name;
            }

            return string.IsNullOrEmpty(name) ? packageId : name + " (" + packageId + ")";
        }

        private static bool IsModLoaded(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                return false;
            }

            string normalized = packageId.ToLowerInvariant();
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (mod != null && mod.PackageId != null && mod.PackageId.ToLowerInvariant() == normalized)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatEfficiency(float? efficiency)
        {
            return efficiency.HasValue ? efficiency.Value.ToString("0.##") : "n/a";
        }

        private static float EstimateRaidPointCost(ImplantPoolEntry entry)
        {
            float baseCost;
            switch (entry.TechTier)
            {
                case ImplantTechTier.Animal:
                    baseCost = 5f;
                    break;
                case ImplantTechTier.Neolithic:
                    baseCost = 8f;
                    break;
                case ImplantTechTier.Medieval:
                    baseCost = 12f;
                    break;
                case ImplantTechTier.Industrial:
                    baseCost = 35f;
                    break;
                case ImplantTechTier.Spacer:
                    baseCost = 75f;
                    break;
                case ImplantTechTier.Ultra:
                    baseCost = 120f;
                    break;
                case ImplantTechTier.Archotech:
                    baseCost = 200f;
                    break;
                default:
                    baseCost = 50f;
                    break;
            }

            float usefulnessFactor;
            switch (entry.Usefulness)
            {
                case ImplantUsefulness.CoreCombat:
                    usefulnessFactor = 1f;
                    break;
                case ImplantUsefulness.SupportCombat:
                    usefulnessFactor = 0.65f;
                    break;
                case ImplantUsefulness.LowCombat:
                    usefulnessFactor = 0.35f;
                    break;
                default:
                    usefulnessFactor = 0.5f;
                    break;
            }

            float efficiencyFactor = entry.Efficiency.HasValue ? Math.Max(0.5f, entry.Efficiency.Value) : 1f;
            return Math.Max(1f, baseCost * usefulnessFactor * efficiencyFactor);
        }

        internal sealed class ImplantTierSummary
        {
            public readonly string TechTier;
            public readonly int DirectImplantCount;

            public ImplantTierSummary(string techTier, int directImplantCount)
            {
                TechTier = techTier;
                DirectImplantCount = directImplantCount;
            }
        }

        internal sealed class ImplantPoolSelection
        {
            public readonly string RequestedTechTier;
            public readonly string BodyPartDefName;
            public readonly string SourceTechTier;
            public readonly bool IsFallback;
            public readonly List<ImplantPoolViewEntry> Entries;

            public ImplantPoolSelection(string requestedTechTier, string bodyPartDefName, string sourceTechTier, bool isFallback, List<ImplantPoolViewEntry> entries)
            {
                RequestedTechTier = requestedTechTier;
                BodyPartDefName = bodyPartDefName;
                SourceTechTier = sourceTechTier;
                IsFallback = isFallback;
                Entries = entries;
            }

            public static ImplantPoolSelection Empty(string requestedTechTier, string bodyPartDefName)
            {
                return new ImplantPoolSelection(requestedTechTier, bodyPartDefName, requestedTechTier, false, new List<ImplantPoolViewEntry>());
            }
        }

        internal sealed class ImplantPoolViewEntry
        {
            public readonly string DefName;
            public readonly string Label;
            public readonly string Usefulness;
            public readonly string BodyPartDefName;
            public readonly string Efficiency;
            public readonly string SourceMod;
            public readonly string PackageId;
            public readonly string FactionScope;

            public ImplantPoolViewEntry(string defName, string label, string usefulness, string bodyPartDefName, string efficiency, string sourceMod, string packageId, string factionScope)
            {
                DefName = defName;
                Label = label;
                Usefulness = usefulness;
                BodyPartDefName = bodyPartDefName;
                Efficiency = efficiency;
                SourceMod = sourceMod;
                PackageId = packageId;
                FactionScope = factionScope;
            }
        }

        internal sealed class ImplantUpgradeCandidate
        {
            public readonly HediffDef HediffDef;
            public readonly string DefName;
            public readonly string Label;
            public readonly string BodyPartDefName;
            public readonly string Usefulness;
            public readonly float EstimatedRaidPointCost;

            public ImplantUpgradeCandidate(HediffDef hediffDef, string defName, string label, string bodyPartDefName, string usefulness, float estimatedRaidPointCost)
            {
                HediffDef = hediffDef;
                DefName = defName;
                Label = label;
                BodyPartDefName = bodyPartDefName;
                Usefulness = usefulness;
                EstimatedRaidPointCost = estimatedRaidPointCost;
            }
        }

        private sealed class ImplantPoolRule
        {
            public readonly string HediffDefName;
            public readonly ImplantTechTier TechTier;
            public readonly ImplantUsefulness Usefulness;
            public readonly string PackageId;
            public readonly List<string> AllowedFactionDefNames;

            public ImplantPoolRule(string hediffDefName, ImplantTechTier techTier, ImplantUsefulness usefulness, string packageId, List<string> allowedFactionDefNames)
            {
                HediffDefName = hediffDefName;
                TechTier = techTier;
                Usefulness = usefulness;
                PackageId = packageId;
                AllowedFactionDefNames = allowedFactionDefNames ?? new List<string>();
            }
        }

        private sealed class ImplantPoolEntry
        {
            public HediffDef HediffDef;
            public string DefName;
            public string Label;
            public ImplantTechTier TechTier;
            public ImplantUsefulness Usefulness;
            public string BodyPartDefName;
            public float? Efficiency;
            public string SourceMod;
            public string PackageId;
            public List<string> AllowedFactionDefNames;
        }

        private sealed class EffectivePool
        {
            public readonly ImplantTechTier SourceTier;
            public readonly List<ImplantPoolEntry> Entries;

            public EffectivePool(ImplantTechTier sourceTier, List<ImplantPoolEntry> entries)
            {
                SourceTier = sourceTier;
                Entries = entries;
            }
        }

        private enum ImplantTechTier
        {
            Animal,
            Neolithic,
            Medieval,
            Industrial,
            Spacer,
            Ultra,
            Archotech
        }

        private enum ImplantUsefulness
        {
            CoreCombat,
            SupportCombat,
            LowCombat,
            Special
        }
    }
}
