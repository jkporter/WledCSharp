namespace Wled.Fx;

/// <summary>
/// The raw cpt-city gradient palette data, transcribed from <c>palettes.cpp</c>.
/// </summary>
/// <remarks>
/// Each array is a sequence of <c>{index, r, g, b}</c> stops ending at index 255;
/// <see cref="Palette16.FromGradient"/> expands one into the 16 slots of a palette.
/// </remarks>
public static partial class Palettes
{
    private static readonly byte[] IbJul01 =
    [
        0, 226,   6,  12,
        94,  26,  96,  78,
        132, 130, 189,  94,
        255, 177,   3,   9,
    ];

    private static readonly byte[] EsVintage57 =
    [
        0,  29,   8,   3,
        53,  76,   1,   0,
        104, 142,  96,  28,
        153, 211, 191,  61,
        255, 117, 129,  42,
    ];

    private static readonly byte[] EsVintage01 =
    [
        0,  41,  18,  24,
        51,  73,   0,  22,
        76, 165, 170,  38,
        101, 255, 189,  80,
        127, 139,  56,  40,
        153,  73,   0,  22,
        229,  41,  18,  24,
        255,  41,  18,  24,
    ];

    private static readonly byte[] EsRivendell15 =
    [
        0,  24,  69,  44,
        101,  73, 105,  70,
        165, 129, 140,  97,
        242, 200, 204, 166,
        255, 200, 204, 166,
    ];

    private static readonly byte[] Rgi15 =
    [
        0,  41,  14,  99,
        31, 128,  24,  74,
        63, 227,  34,  50,
        95, 132,  31,  76,
        127,  47,  29, 102,
        159, 109,  47, 101,
        191, 176,  66, 100,
        223, 129,  57, 104,
        255,  84,  48, 108,
    ];

    private static readonly byte[] Retro216 =
    [
        0, 222, 191,   8,
        255, 117,  52,   1,
    ];

    private static readonly byte[] Analogous1 =
    [
        0,  38,   0, 255,
        63,  86,   0, 255,
        127, 139,   0, 255,
        191, 196,   0, 117,
        255, 255,   0,   0,
    ];

    private static readonly byte[] EsPinksplash08 =
    [
        0, 186,  63, 255,
        127, 227,   9,  85,
        175, 234, 205, 213,
        221, 205,  38, 176,
        255, 205,  38, 176,
    ];

    private static readonly byte[] EsOceanBreeze036 =
    [
        0,  16,  48,  51,
        89,  27, 166, 175,
        153, 197, 233, 255,
        255,   0, 145, 152,
    ];

    private static readonly byte[] Departure =
    [
        0,  53,  34,   0,
        42,  86,  51,   0,
        63, 147, 108,  49,
        84, 212, 166, 108,
        106, 235, 212, 180,
        116, 255, 255, 255,
        138, 191, 255, 193,
        148,  84, 255,  88,
        170,   0, 255,   0,
        191,   0, 192,   0,
        212,   0, 128,   0,
        255,   0, 128,   0,
    ];

    private static readonly byte[] EsLandscape64 =
    [
        0,   0,   0,   0,
        37,  31,  89,  19,
        76,  72, 178,  43,
        127, 150, 235,   5,
        128, 186, 234, 119,
        130, 222, 233, 252,
        153, 197, 219, 231,
        204, 132, 179, 253,
        255,  28, 107, 225,
    ];

    private static readonly byte[] EsLandscape33 =
    [
        0,  12,  45,   0,
        19, 101,  86,   2,
        38, 207, 128,   4,
        63, 243, 197,  18,
        66, 109, 196, 146,
        255,   5,  39,   7,
    ];

    private static readonly byte[] Rainbowsherbet =
    [
        0, 255, 102,  41,
        43, 255, 140,  90,
        86, 255,  51,  90,
        127, 255, 153, 169,
        170, 255, 255, 249,
        209, 113, 255,  85,
        255, 157, 255, 137,
    ];

    private static readonly byte[] Gr65Hult =
    [
        0, 251, 216, 252,
        48, 255, 192, 255,
        89, 239,  95, 241,
        160,  51, 153, 217,
        216,  24, 184, 174,
        255,  24, 184, 174,
    ];

    private static readonly byte[] Gr64Hult =
    [
        0,  24, 184, 174,
        66,   8, 162, 150,
        104, 124, 137,   7,
        130, 178, 186,  22,
        150, 124, 137,   7,
        201,   6, 156, 144,
        239,   0, 128, 117,
        255,   0, 128, 117,
    ];

    private static readonly byte[] GmtDrywet =
    [
        0, 119,  97,  33,
        42, 235, 199,  88,
        84, 169, 238, 124,
        127,  37, 238, 232,
        170,   7, 120, 236,
        212,  27,   1, 175,
        255,   4,  51, 101,
    ];

    private static readonly byte[] Ib15 =
    [
        0, 177, 160, 199,
        72, 205, 158, 149,
        89, 233, 155, 101,
        107, 255,  95,  63,
        141, 192,  98, 109,
        255, 132, 101, 159,
    ];

    private static readonly byte[] Tertiary01 =
    [
        0,   0,  25, 255,
        63,  38, 140, 117,
        127,  86, 255,   0,
        191, 167, 140,  19,
        255, 255,  25,  41,
    ];

    private static readonly byte[] Lava =
    [
        0,   0,   0,   0,
        46,  77,   0,   0,
        96, 177,   0,   0,
        108, 196,  38,   9,
        119, 215,  76,  19,
        146, 235, 115,  29,
        174, 255, 153,  41,
        188, 255, 178,  41,
        202, 255, 204,  41,
        218, 255, 230,  41,
        234, 255, 255,  41,
        244, 255, 255, 143,
        255, 255, 255, 255,
    ];

    private static readonly byte[] FierceIce =
    [
        0,   0,   0,   0,
        59,   0,  51, 117,
        119,   0, 102, 255,
        149,  38, 153, 255,
        180,  86, 204, 255,
        217, 167, 230, 255,
        255, 255, 255, 255,
    ];

    private static readonly byte[] Colorfull =
    [
        0,  61, 155,  44,
        25,  95, 174,  77,
        60, 132, 193, 113,
        93, 154, 166, 125,
        106, 175, 138, 136,
        109, 183, 121, 137,
        113, 194, 104, 138,
        116, 225, 179, 165,
        124, 255, 255, 192,
        168, 167, 218, 203,
        255,  84, 182, 215,
    ];

    private static readonly byte[] PinkPurple =
    [
        0,  79,  32, 109,
        25,  90,  40, 117,
        51, 102,  48, 124,
        76, 141, 135, 185,
        102, 180, 222, 248,
        109, 208, 236, 252,
        114, 237, 250, 255,
        122, 206, 200, 239,
        149, 177, 149, 222,
        183, 187, 130, 203,
        255, 198, 111, 184,
    ];

    private static readonly byte[] SunsetReal =
    [
        0, 181,   0,   0,
        22, 218,  85,   0,
        51, 255, 170,   0,
        85, 211,  85,  77,
        135, 167,   0, 169,
        198,  73,   0, 188,
        255,   0,   0, 207,
    ];

    private static readonly byte[] SunsetYellow =
    [
        0,  61, 135, 184,
        36, 129, 188, 169,
        87, 203, 241, 155,
        100, 228, 237, 141,
        107, 255, 232, 127,
        115, 251, 202, 130,
        120, 248, 172, 133,
        128, 251, 202, 130,
        180, 255, 232, 127,
        223, 255, 242, 120,
        255, 255, 252, 113,
    ];

    private static readonly byte[] Beech =
    [
        0, 255, 254, 236,
        12, 255, 254, 236,
        22, 255, 254, 236,
        26, 223, 224, 178,
        28, 192, 195, 124,
        28, 176, 255, 231,
        50, 123, 251, 236,
        71,  74, 246, 241,
        93,  33, 225, 228,
        120,   0, 204, 215,
        133,   4, 168, 178,
        136,  10, 132, 143,
        136,  51, 189, 212,
        208,  23, 159, 201,
        255,   0, 129, 190,
    ];

    private static readonly byte[] AnotherSunset =
    [
        0, 175, 121,  62,
        29, 128, 103,  60,
        68,  84,  84,  58,
        68, 248, 184,  55,
        97, 239, 204,  93,
        124, 230, 225, 133,
        178, 102, 125, 129,
        255,   0,  26, 125,
    ];

    private static readonly byte[] EsAutumn19 =
    [
        0,  90,  14,   5,
        51, 139,  41,  13,
        84, 180,  70,  17,
        104, 192, 202, 125,
        112, 177, 137,   3,
        122, 190, 200, 131,
        124, 192, 202, 124,
        135, 177, 137,   3,
        142, 194, 203, 118,
        163, 177,  68,  17,
        204, 128,  35,  12,
        249,  74,   5,   2,
        255,  74,   5,   2,
    ];

    private static readonly byte[] BlackBlueMagentaWhite =
    [
        0,   0,   0,   0,
        42,   0,   0, 117,
        84,   0,   0, 255,
        127, 113,   0, 255,
        170, 255,   0, 255,
        212, 255, 128, 255,
        255, 255, 255, 255,
    ];

    private static readonly byte[] BlackMagentaRed =
    [
        0,   0,   0,   0,
        63, 113,   0, 117,
        127, 255,   0, 255,
        191, 255,   0, 117,
        255, 255,   0,   0,
    ];

    private static readonly byte[] BlackRedMagentaYellow =
    [
        0,   0,   0,   0,
        42, 113,   0,   0,
        84, 255,   0,   0,
        127, 255,   0, 117,
        170, 255,   0, 255,
        212, 255, 128, 117,
        255, 255, 255,   0,
    ];

    private static readonly byte[] BlueCyanYellow =
    [
        0,   0,   0, 255,
        63,   0, 128, 255,
        127,   0, 255, 255,
        191, 113, 255, 117,
        255, 255, 255,   0,
    ];

    private static readonly byte[] OrangeTeal =
    [
        0,   0,150, 92,
        55,   0,150, 92,
        200, 255, 72,  0,
        255, 255, 72,  0,
    ];

    private static readonly byte[] Tiamat =
    [
        0,   1,  2, 14,
        33,   2,  5, 35,
        100,  13,135, 92,
        120,  43,255,193,
        140, 247,  7,249,
        160, 193, 17,208,
        180,  39,255,154,
        200,   4,213,236,
        220,  39,252,135,
        240, 193,213,253,
        255, 255,249,255,
    ];

    private static readonly byte[] AprilNight =
    [
        0,   1,  5, 45,
        10,   1,  5, 45,
        25,   5,169,175,
        40,   1,  5, 45,
        61,   1,  5, 45,
        76,  45,175, 31,
        91,   1,  5, 45,
        112,   1,  5, 45,
        127, 249,150,  5,
        143,   1,  5, 45,
        162,   1,  5, 45,
        178, 255, 92,  0,
        193,   1,  5, 45,
        214,   1,  5, 45,
        229, 223, 45, 72,
        244,   1,  5, 45,
        255,   1,  5, 45,
    ];

    private static readonly byte[] Orangery =
    [
        0, 255, 95, 23,
        30, 255, 82,  0,
        60, 223, 13,  8,
        90, 144, 44,  2,
        120, 255,110, 17,
        150, 255, 69,  0,
        180, 158, 13, 11,
        210, 241, 82, 17,
        255, 213, 37,  4,
    ];

    private static readonly byte[] C9 =
    [
        0, 184,  4,  0,
        60, 184,  4,  0,
        65, 144, 44,  2,
        125, 144, 44,  2,
        130,   4, 96,  2,
        190,   4, 96,  2,
        195,   7,  7, 88,
        255,   7,  7, 88,
    ];

    private static readonly byte[] Sakura =
    [
        0, 196, 19, 10,
        65, 255, 69, 45,
        130, 223, 45, 72,
        195, 255, 82,103,
        255, 223, 13, 17,
    ];

    private static readonly byte[] Aurora =
    [
        0,   1,  5, 45,
        64,   0,200, 23,
        128,   0,255,  0,
        170,   0,243, 45,
        200,   0,135,  7,
        255,   1,  5, 45,
    ];

    private static readonly byte[] Atlantica =
    [
        0,   0, 28,112,
        50,  32, 96,255,
        100,   0,243, 45,
        150,  12, 95, 82,
        200,  25,190, 95,
        255,  40,170, 80,
    ];

    private static readonly byte[] C92 =
    [
        0,   6, 126,   2,
        45,   6, 126,   2,
        46,   4,  30, 114,
        90,   4,  30, 114,
        91, 255,   5,   0,
        135, 255,   5,   0,
        136, 196,  57,   2,
        180, 196,  57,   2,
        181, 137,  85,   2,
        255, 137,  85,   2,
    ];

    private static readonly byte[] C9New =
    [
        0, 255,   5,   0,
        60, 255,   5,   0,
        61, 196,  57,   2,
        120, 196,  57,   2,
        121,   6, 126,   2,
        180,   6, 126,   2,
        181,   4,  30, 114,
        255,   4,  30, 114,
    ];

    private static readonly byte[] Temperature =
    [
        0,  20,  92, 171,
        14,  15, 111, 186,
        28,   6, 142, 211,
        42,   2, 161, 227,
        56,  16, 181, 239,
        70,  38, 188, 201,
        84,  86, 204, 200,
        99, 139, 219, 176,
        113, 182, 229, 125,
        127, 196, 230,  63,
        141, 241, 240,  22,
        155, 254, 222,  30,
        170, 251, 199,   4,
        184, 247, 157,   9,
        198, 243, 114,  15,
        226, 213,  30,  29,
        240, 151,  38,  35,
        255, 151,  38,  35,
    ];

    private static readonly byte[] RetroClown =
    [
        0, 242, 168,  38,
        117, 226,  78,  80,
        255, 161,  54, 225,
    ];

    private static readonly byte[] Candy =
    [
        0, 243, 242,  23,
        15, 242, 168,  38,
        142, 111,  21, 151,
        198,  74,  22, 150,
        255,   0,   0, 117,
    ];

    private static readonly byte[] ToxyReaf =
    [
        0,   2, 239, 126,
        255, 145,  35, 217,
    ];

    private static readonly byte[] FairyReaf =
    [
        0, 220,  19, 187,
        160,  12, 225, 219,
        219, 203, 242, 223,
        255, 255, 255, 255,
    ];

    private static readonly byte[] SemiBlue =
    [
        0,   0,   0,   0,
        12,  24,   4,  38,
        53,  55,   8,  84,
        80,  43,  48, 159,
        119,  31,  89, 237,
        145,  50,  59, 166,
        186,  71,  30,  98,
        233,  31,  15,  45,
        255,   0,   0,   0,
    ];

    private static readonly byte[] PinkCandy =
    [
        0, 255, 255, 255,
        45,  50,  64, 255,
        112, 242,  16, 186,
        140, 255, 255, 255,
        155, 242,  16, 186,
        196, 116,  13, 166,
        255, 255, 255, 255,
    ];

    private static readonly byte[] RedReaf =
    [
        0,  36,  68, 114,
        104, 149, 195, 248,
        188, 255,   0,   0,
        255,  94,  14,   9,
    ];

    private static readonly byte[] AquaFlash =
    [
        0,   0,   0,   0,
        66, 130, 242, 245,
        96, 255, 255,  53,
        124, 255, 255, 255,
        153, 255, 255,  53,
        188, 130, 242, 245,
        255,   0,   0,   0,
    ];

    private static readonly byte[] YelbluHot =
    [
        0,  43,  30,  57,
        58,  73,   0, 119,
        122,  87,   0,  74,
        158, 197,  57,  22,
        183, 218, 117,  27,
        219, 239, 177,  32,
        255, 246, 247,  27,
    ];

    private static readonly byte[] LiteLight =
    [
        0,   0,   0,   0,
        9,  20,  21,  22,
        40,  46,  43,  49,
        66,  46,  43,  49,
        101,  61,  16,  65,
        255,   0,   0,   0,
    ];

    private static readonly byte[] RedFlash =
    [
        0,   0,   0,   0,
        99, 242,  12,   8,
        130, 253, 228, 163,
        155, 242,  12,   8,
        255,   0,   0,   0,
    ];

    private static readonly byte[] BlinkRed =
    [
        0,   4,   7,   4,
        43,  40,  25,  62,
        76,  61,  15,  36,
        109, 207,  39,  96,
        127, 255, 156, 184,
        165, 185,  73, 207,
        204, 105,  66, 240,
        255,  77,  29,  78,
    ];

    private static readonly byte[] RedShift =
    [
        0,  98,  22,  93,
        45, 103,  22,  73,
        99, 192,  45,  56,
        132, 235, 187,  59,
        175, 228,  85,  26,
        201, 228,  56,  48,
        255,   2,   0,   2,
    ];

    private static readonly byte[] RedTide =
    [
        0, 251,  46,   0,
        28, 255, 139,  25,
        43, 246, 158,  63,
        58, 246, 216, 123,
        84, 243,  94,  10,
        114, 177,  65,  11,
        140, 255, 241, 115,
        168, 177,  65,  11,
        196, 250, 233, 158,
        216, 255,  94,   6,
        255, 126,   8,   4,
    ];

    private static readonly byte[] Candy2 =
    [
        0, 109, 102, 102,
        25,  42,  49,  71,
        48, 121,  96,  84,
        73, 241, 214,  26,
        89, 216, 104,  44,
        130,  42,  49,  71,
        163, 255, 177,  47,
        186, 241, 214,  26,
        211, 109, 102, 102,
        255,  20,  19,  13,
    ];

    private static readonly byte[] Trafficlight =
    [
        0,   0,   0, 0,
        85,   0, 255, 0,
        170, 255, 255, 0,
        255, 255,   0, 0,
    ];

    private static readonly byte[] Aurora2 =
    [
        0,  17, 177,  13,
        64, 121, 242,   5,
        128,  25, 173, 121,
        192, 250,  77, 127,
        255, 171, 101, 221,
    ];

    /// <summary>The raw gradient blobs in ID order (13-71); see <see cref="Palette16.FromGradient"/>.</summary>
    public static readonly byte[][] Gradients =
    [
        SunsetReal, EsRivendell15, EsOceanBreeze036, Rgi15, Retro216, Analogous1, EsPinksplash08,
        SunsetYellow, AnotherSunset, Beech, EsVintage01, Departure, EsLandscape64, EsLandscape33,
        Rainbowsherbet, Gr65Hult, Gr64Hult, GmtDrywet, IbJul01, EsVintage57, Ib15, Tertiary01, Lava,
        FierceIce, Colorfull, PinkPurple, EsAutumn19, BlackBlueMagentaWhite, BlackMagentaRed,
        BlackRedMagentaYellow, BlueCyanYellow, OrangeTeal, Tiamat, AprilNight, Orangery, C9, Sakura,
        Aurora, Atlantica, C92, C9New, Temperature, Aurora2, RetroClown, Candy, ToxyReaf, FairyReaf,
        SemiBlue, PinkCandy, RedReaf, AquaFlash, YelbluHot, LiteLight, RedFlash, BlinkRed, RedShift,
        RedTide, Candy2, Trafficlight,
    ];
}
