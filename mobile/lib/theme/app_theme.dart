import 'package:flutter/material.dart';

/// Colors lifted from UI/StudyHive Mobile UI (offline).html so the built app
/// matches the supplied mockup instead of inventing a new palette.
class AppColors {
  static const accent = Color(0xFF5980A6);
  static const accent700 = Color(0xFF416180);
  static const accent100 = Color(0xFFEEF6FF);
  static const accent2 = Color(0xFF728FAB);
  static const background = Color(0xFFF2F2F3);
  static const surface = Color(0xFFE9E9EA);
  static const text = Color(0xFF1D1F20);
}

ThemeData buildAppTheme() {
  return ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: AppColors.accent,
      surface: AppColors.surface,
    ),
    scaffoldBackgroundColor: AppColors.background,
    appBarTheme: const AppBarTheme(
      backgroundColor: AppColors.accent700,
      foregroundColor: Colors.white,
    ),
  );
}
