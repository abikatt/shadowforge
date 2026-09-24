namespace ShadowForge.Scene.Script;

/// <summary>
/// Registry of script opcodes: names, fixed sizes and argument presentation.
/// </summary>
public static class OpcodeTable
{
    public const uint Label = 5000;
    public const uint SetVariable = 5003;
    public const uint IfExtended = 5012;
    public const uint GotoLabel = 5013;
    public const uint Battle = 5016;
    public const uint EndScript = 5023;
    public const uint Overflow = 5042;
    public const uint Comment = 5050;
    public const uint InitBegin = 5057;
    public const uint InitEnd = 5058;
    public const uint FlagGetSet = 5063;

    public const uint FirstOpcode = 5000;
    public const uint LastOpcode = 5100;

    /// <summary>
    /// Bit 31 of an opcode word marks the instruction as disabled.
    /// </summary>
    public const uint DisabledFlag = 0x80000000;

    /// <summary>
    /// Largest instruction the binary reader decodes, header included. A larger size word is read as raw data.
    /// </summary>
    public const int MaxInstructionSize = 512;

    public const int MaxWords = (MaxInstructionSize - 8) / 4;

    /// <summary>
    /// Party member names used by character-index arguments. 0xFFFFFFFF selects the whole party.
    /// </summary>
    public static readonly IReadOnlyDictionary<uint, string> CharNames = new Dictionary<uint, string>
    {
        [0] = "shu", [1] = "jiro", [2] = "kluke", [3] = "marumaro", [4] = "zola", [0xFFFFFFFF] = "all",
    };

    private static ArgSpec I(string n) => new(n, ArgKind.Int);
    private static ArgSpec H(string n) => new(n, ArgKind.Hex);
    private static ArgSpec F(string n) => new(n, ArgKind.Float);
    private static ArgSpec V(string n) => new(n, ArgKind.Var);
    private static ArgSpec L(string n) => new(n, ArgKind.Label);
    private static ArgSpec C(string n) => new(n, ArgKind.Enum, CharNames);
    private static readonly ArgSpec[] None = [];

    private static readonly Dictionary<uint, OpcodeSpec> ByOpcode = Build();
    private static readonly Dictionary<string, OpcodeSpec> ByName =
        ByOpcode.Values.ToDictionary(s => s.Name, StringComparer.Ordinal);

    private static Dictionary<uint, OpcodeSpec> Build()
    {
        var d = new Dictionary<uint, OpcodeSpec>();
        void Add(uint op, string name, int words, params ArgSpec[] args) => d[op] = new OpcodeSpec(op, name, words, args);

        Add(5000, "label", 2, L("id"));
        Add(5001, "show_message", 6, I("msg"), I("p1"), I("auto_advance"), I("delay"));
        Add(5003, "set_variable", 6, V("dest"), I("op"), I("value_type"), I("value"));
        Add(5004, "give_item", 6, I("item"), I("add"), I("qty"), I("popup"), I("p4"), H("flags"));
        Add(5005, "give_gold", 2, H("mode"), I("amount"));
        Add(5006, "set_ailment", 6, C("chara"), I("ailment"), I("active"));
        Add(5008, "heal", 6, C("chara"), I("target"), H("mode"), I("amount"));
        Add(5010, "formation", 6, C("chara"), I("action"), I("level_ref"), I("level"), I("class_slot"));
        Add(5012, "if_extended", 10, I("check_type"), I("sub_type"), I("operand"), I("compare_value"), I("compare_op"), I("true_action"), L("true_label"), I("false_action"), L("false_label"));
        Add(5013, "goto_label", 2, L("id"));
        Add(5014, "shop_open", 2, H("mode"), I("shop"));
        Add(5016, "battle", 14, I("entry"), I("p1"), I("p2"), I("p3"), H("flags"), I("win_action"), L("win_label"), I("lose_action"), L("lose_label"));
        Add(5017, "play_bgm", 6, I("stop"), I("bgm"), H("mode"));
        Add(5018, "play_se", 14, H("flags"), I("loops"), I("p2"), F("x"), F("y"), F("z"), I("p6"), H("name0"), H("name1"), H("name2"), H("name3"), H("name4"), H("name5"), H("name6"));
        Add(5020, "wait", 6, I("type"), I("frames"));
        Add(5021, "map_change", 10, I("area_type"), I("stage"), H("flags"), F("x"), F("z"), F("y"), F("angle"), I("entry_point"), I("transition"), I("npc_ref"));
        Add(5022, "game_over", 2);
        Add(5023, "end_script", 2);
        Add(5024, "set_chapter_flag", 2, I("chapter"));
        Add(5025, "transition2", 10, I("has_dest"), I("dest"));
        Add(5026, "screen_fade", 10, I("type"), I("frames"), H("color"), I("p3"), I("p4"), I("p5"), H("p6"), I("alt_enable"), I("alt_target"), I("alt_flag"));
        Add(5027, "npc_action", 6, I("target_type"), I("action"), H("flags"), I("npc"));
        Add(5028, "move_character", 10, I("target_type"), I("move_type"), F("x"), F("y"), F("z"), F("rotation"), I("param"), I("npc"), H("flags"), V("callback_var"));
        Add(5029, "player_teleport", 6, F("x"), F("y"), F("z"), F("rotation"), I("p4"), I("warp"));
        Add(5030, "battle_damage", 6, C("chara"), I("mode"), I("damage"));
        Add(5031, "fade_transition", 6, I("msg"), I("param1"), I("param2"), I("param3"), I("play_transition"), I("area_flags"));
        Add(5032, "event_scene", 6, I("major"), I("p1"), I("minor"), I("p3"), I("behavior"));
        Add(5033, "character_walk", 6);
        Add(5034, "give_medal", 2, H("mode"), I("amount"));
        Add(5035, "character_setup", 10, C("chara"), I("direction"));
        Add(5036, "dismiss_party", 6, I("npc"), H("flags"));
        Add(5037, "character_equip", 6, C("chara"), I("change_leader"));
        Add(5039, "character_position", 10, I("p0"), I("npc"), I("use_warp"), I("warp"), F("x"), F("y"), F("z"), F("rotation"));
        Add(5040, "set_field_state", 6, I("mode"), I("field_type"));
        Add(5041, "character_visible", 6, I("visible"));
        Add(5042, "if_overflow", 6, I("check_type"), I("item"), I("amount"), L("label"), H("flags"));
        Add(5043, "visual_effect", 10, I("mode"), I("npc"), I("action"), I("sub_action"));
        Add(5044, "special_op", 2, I("mode"));
        Add(5045, "close_dialogue", 2);
        Add(5046, "checkpoint", 2);
        Add(5047, "warp_activate", 6, I("warp_point"), H("mode"));
        Add(5048, "render_state", 6, I("layer"));
        Add(5049, "camera_point", 6, I("set_position"), I("camera_point"));
        Add(5050, "comment", 14);
        Add(5051, "minigame_start", 6, I("minigame"), I("config1"), I("config2"));
        Add(5052, "camera_target", 6);
        Add(5053, "scene_script", 6);
        Add(5054, "goto_title", 2);
        Add(5055, "shadow_toggle", 6, C("chara"), I("shadow"));
        Add(5056, "party_shadow_class", 6, I("shadow_class"));
        Add(5057, "init_begin", 2);
        Add(5058, "init_end", 2);
        Add(5059, "character_effect", 6, I("target_type"), I("npc"), I("effect"));
        Add(5060, "effect", 6, I("target_type"), I("npc"), I("p2"), H("hash"));
        Add(5061, "animation_trigger", 6);
        Add(5062, "camera_control", 14, I("mode"), I("p1"), I("frames"), F("eye_x"), F("eye_y"), F("eye_z"), F("target_x"), F("target_y"), F("target_z"), F("yaw"));
        Add(5063, "flag_get_set", 6, I("flag"), I("direction"), I("value_or_dest"));
        Add(5064, "start_qte", 6, I("qte"));
        Add(5065, "check_animation", 6);
        Add(5066, "shop_inn", 2);
        Add(5067, "show_message_b", 6, I("msg"), I("p1"), I("auto_advance"), I("delay"));
        Add(5068, "transition2_b", 10, I("has_dest"), I("dest"));
        Add(5070, "fade_transition_b", 6, I("msg"), I("param1"), I("param2"), I("param3"), I("play_transition"), I("area_flags"));
        Add(5071, "set_field_script", 6);
        Add(5072, "set_abilities", 6, I("target_mode"), C("chara"), H("mask"));
        Add(5073, "lookup_map", 2);
        Add(5074, "equip_skill", 6);
        Add(5075, "open_save_menu", 2);
        Add(5076, "character_shadow", 6);
        Add(5077, "character_speed", 6);
        Add(5078, "npc_walk_speed", 6);
        Add(5079, "face_target", 10, I("specific"), I("chara"), I("target_type"), I("target"), F("x"), F("y"), F("z"));
        Add(5080, "effect_spawn", 10);
        Add(5081, "camera_shake", 6, I("active"), I("frames"), F("direction"), F("magnitude"));
        Add(5083, "party_gather", 6, F("angle"), H("flags"));
        Add(5084, "quest_patch", 6, I("quest"));
        Add(5085, "set_battle_result", 2);
        Add(5086, "give_item_special", 6);
        Add(5087, "movie_play", 6);
        Add(5088, "save_position", 2, I("mode"));
        Add(5089, "load_event_pack", 6);
        Add(5090, "npc_walk_state", 6);
        Add(5091, "shadow_state", 6, I("mode"));
        Add(5093, "get_variable", 2, V("dest"));
        Add(5094, "select_menu", 6);
        Add(5095, "treasure_chest", 6);
        Add(5096, "if_chest", 10, I("check_type"), I("sub_type"), I("operand"), I("compare_value"), I("compare_op"), I("true_action"), L("true_label"), I("false_action"), L("false_label"));
        Add(5097, "set_variable_chest", 6, V("dest"), I("op"), I("value_type"), I("value"));
        Add(5098, "special_query", 2);
        Add(5100, "open_book", 6, I("p0"), I("book"));
        return d;
    }

    /// <summary>
    /// True when the opcode, ignoring the disabled bit, is in FirstOpcode..LastOpcode.
    /// </summary>
    public static bool IsValid(uint opcode) => (opcode & ~DisabledFlag) is >= FirstOpcode and <= LastOpcode;

    /// <summary>
    /// Spec for an opcode, ignoring the disabled bit. An opcode without a table row gets a six-word op_N spec.
    /// </summary>
    public static OpcodeSpec Get(uint opcode)
    {
        uint op = opcode & ~DisabledFlag;
        if (!IsValid(op))
            throw new ArgumentOutOfRangeException(nameof(opcode), op, "opcode outside 5000..5100");
        return ByOpcode.TryGetValue(op, out var spec) ? spec : new OpcodeSpec(op, $"op_{op}", 6, None);
    }

    /// <summary>
    /// Look up a spec by its BDSL name, including op_NNNN names.
    /// </summary>
    public static bool TryGetByName(string name, out OpcodeSpec spec)
    {
        if (ByName.TryGetValue(name, out spec!)) return true;
        if (name.StartsWith("op_", StringComparison.Ordinal) && uint.TryParse(name.AsSpan(3), out uint op)
            && op is >= FirstOpcode and <= LastOpcode)
        {
            spec = Get(op);
            return true;
        }
        spec = null!;
        return false;
    }
}
