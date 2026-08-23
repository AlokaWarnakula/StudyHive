import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Design tokens copied verbatim from the UI reference
/// (UI/StudyHive Mobile UI (offline).html, the :root block) so the built app
/// matches the supplied mockup instead of inventing a new palette. The web
/// client carries the same values in web/src/index.css.
class AppColors {
  // Ground
  static const bg = Color(0xFFF2F2F3);
  static const surface = Color(0xFFE9E9EA);
  static const text = Color(0xFF1D1F20);

  /// color-mix(in srgb, --color-text 16%, transparent) flattened onto [bg].
  static const divider = Color(0xFFD0D0D1);

  /// --color-text at 55% — the reference .text-muted / .lbl ink.
  static const muted = Color(0xFF7D7E7F);

  /// --color-text at 58% — .fnote.
  static const note = Color(0xFF767879);

  /// --color-text at 70% — the label above an .input.
  static const fieldLabel = Color(0xFF5D5E5F);

  // Accent ramp
  static const accent = Color(0xFF5980A6);
  static const accent100 = Color(0xFFEEF6FF);
  static const accent200 = Color(0xFFD6EBFF);
  static const accent300 = Color(0xFFB5D9FD);
  static const accent400 = Color(0xFF94BCE3);
  static const accent500 = Color(0xFF749DC4);
  static const accent600 = Color(0xFF597EA3);
  static const accent700 = Color(0xFF416180);
  static const accent800 = Color(0xFF2C455D);
  static const accent900 = Color(0xFF1D2D3D);

  static const accent2 = Color(0xFF728FAB);

  // Neutral ramp
  static const neutral100 = Color(0xFFF5F5F8);
  static const neutral200 = Color(0xFFE7E7EA);
  static const neutral300 = Color(0xFFD4D4D7);
  static const neutral400 = Color(0xFFB7B7BA);
  static const neutral500 = Color(0xFF98989B);
  static const neutral600 = Color(0xFF7A7A7D);
  static const neutral700 = Color(0xFF5D5D60);
  static const neutral800 = Color(0xFF424244);
  static const neutral900 = Color(0xFF2B2B2D);

  /// The reference draws no red/green/amber — status is carried entirely by the
  /// three tag tones. [danger] is kept for genuine failure text only.
  static const danger = Color(0xFFA63E4A);

  static const surfaceBright = Color(0xFFFFFFFF);
}

/// --space-* from the reference (a 3.4px base).
class AppSpace {
  static const s1 = 3.4;
  static const s2 = 6.8;
  static const s3 = 10.2;
  static const s4 = 13.6;
  static const s6 = 20.4;
  static const s8 = 27.2;
}

/// --radius-*. Tiles are square; only controls take the 4px radius.
class AppRadius {
  static const sm = BorderRadius.all(Radius.circular(2));
  static const md = BorderRadius.all(Radius.circular(4));
  static const tag = BorderRadius.all(Radius.circular(3));
  static const none = BorderRadius.zero;
}

/// Body copy is Barlow; every heading and control label is Barlow Condensed 600
/// (--font-heading / --font-heading-weight).
TextStyle headingStyle({
  required double fontSize,
  Color? color,
  double? height,
  double? letterSpacing,
  FontWeight fontWeight = FontWeight.w600,
}) =>
    GoogleFonts.barlowCondensed(
      fontSize: fontSize,
      fontWeight: fontWeight,
      color: color ?? AppColors.text,
      height: height,
      letterSpacing: letterSpacing,
    );

ThemeData buildAppTheme() {
  final bodyTextTheme = GoogleFonts.barlowTextTheme().apply(
    bodyColor: AppColors.text,
    displayColor: AppColors.text,
  );
  final textTheme = bodyTextTheme.copyWith(
    displayLarge: GoogleFonts.barlowCondensed(
        textStyle: bodyTextTheme.displayLarge, fontWeight: FontWeight.w600),
    displayMedium: GoogleFonts.barlowCondensed(
        textStyle: bodyTextTheme.displayMedium, fontWeight: FontWeight.w600),
    displaySmall: GoogleFonts.barlowCondensed(
        textStyle: bodyTextTheme.displaySmall, fontWeight: FontWeight.w600),
    headlineLarge: GoogleFonts.barlowCondensed(
        textStyle: bodyTextTheme.headlineLarge, fontWeight: FontWeight.w600),
    headlineMedium: GoogleFonts.barlowCondensed(
        textStyle: bodyTextTheme.headlineMedium, fontWeight: FontWeight.w600),
    headlineSmall: GoogleFonts.barlowCondensed(
        textStyle: bodyTextTheme.headlineSmall, fontWeight: FontWeight.w600),
    titleLarge: GoogleFonts.barlowCondensed(
        textStyle: bodyTextTheme.titleLarge, fontWeight: FontWeight.w600),
    // body { font-size: 15px; line-height: 1.55 }
    bodyMedium: bodyTextTheme.bodyMedium!.copyWith(fontSize: 15, height: 1.55),
  );

  return ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: AppColors.accent,
      primary: AppColors.accent,
      surface: AppColors.surface,
    ),
    scaffoldBackgroundColor: AppColors.bg,
    textTheme: textTheme,
    appBarTheme: AppBarTheme(
      backgroundColor: AppColors.bg,
      foregroundColor: AppColors.text,
      centerTitle: false,
      elevation: 0,
      scrolledUnderElevation: 0,
      surfaceTintColor: Colors.transparent,
      // .mtop h4 { font-size: 21px }
      titleTextStyle: headingStyle(fontSize: 21, letterSpacing: -0.3),
      shape: const Border(bottom: BorderSide(color: AppColors.divider)),
    ),
    cardTheme: const CardThemeData(
      color: Colors.transparent,
      surfaceTintColor: Colors.transparent,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: AppRadius.none,
        side: BorderSide(color: AppColors.divider),
      ),
    ),
    inputDecorationTheme: const InputDecorationTheme(
      filled: true,
      fillColor: AppColors.surface,
      border: OutlineInputBorder(
        borderRadius: AppRadius.md,
        borderSide: BorderSide(color: AppColors.divider),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: AppRadius.md,
        borderSide: BorderSide(color: AppColors.divider),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: AppRadius.md,
        borderSide: BorderSide(color: AppColors.accent),
      ),
      contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      labelStyle: TextStyle(color: AppColors.fieldLabel, fontSize: 14),
      hintStyle: TextStyle(color: AppColors.muted, fontSize: 14),
    ),
    // .btn-primary { background: --color-accent; color: --color-bg }
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: AppColors.accent,
        foregroundColor: AppColors.bg,
        disabledBackgroundColor: AppColors.accent.withValues(alpha: 0.45),
        disabledForegroundColor: AppColors.bg,
        minimumSize: const Size(0, 48),
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.md),
        textStyle: headingStyle(fontSize: 17),
      ),
    ),
    // .btn-secondary { border-color: --color-divider } — ink stays --color-text.
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.text,
        minimumSize: const Size(0, 48),
        side: const BorderSide(color: AppColors.divider),
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.md),
        textStyle: headingStyle(fontSize: 17),
      ),
    ),
    // .btn-ghost { color: --color-accent }
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: AppColors.accent,
        minimumSize: const Size(0, 48),
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.md),
        textStyle: headingStyle(fontSize: 17),
      ),
    ),
    chipTheme: const ChipThemeData(
      shape: RoundedRectangleBorder(
        borderRadius: AppRadius.tag,
        side: BorderSide(color: AppColors.divider),
      ),
    ),
    dialogTheme: const DialogThemeData(
      backgroundColor: AppColors.bg,
      shape: RoundedRectangleBorder(borderRadius: AppRadius.none),
    ),
    dividerTheme: const DividerThemeData(color: AppColors.divider, space: 1),
    progressIndicatorTheme: const ProgressIndicatorThemeData(
      color: AppColors.accent,
      linearTrackColor: AppColors.neutral300,
    ),
  );
}
