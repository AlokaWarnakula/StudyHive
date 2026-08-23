import 'package:flutter/material.dart';

import '../../data/demo_seed.dart';
import '../../theme/app_theme.dart';
import '../../widgets/studyhive_ui.dart';

/// M-15 "Checked in" — the success state. The booking moves to Completed once
/// the slot ends.
class CheckedInScreen extends StatelessWidget {
  const CheckedInScreen({super.key});

  @override
  Widget build(BuildContext context) {
    if (!demoPreviewEnabled) {
      return Scaffold(
        appBar: AppBar(title: const Text('StudyHive')),
        body: const PreviewUnavailable(
          message: 'Check-in details will appear after a successful QR scan.',
        ),
      );
    }

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Center(
                  child: Container(
                    width: 72,
                    height: 72,
                    decoration: BoxDecoration(
                        border: Border.all(color: AppColors.accent, width: 2)),
                    child: const Icon(Icons.check,
                        size: 26, color: AppColors.accent700),
                  ),
                ),
                const SizedBox(height: 18),
                Text('You are checked in',
                    textAlign: TextAlign.center,
                    style: headingStyle(
                        fontSize: 32, height: 1.12, letterSpacing: -0.5)),
                const SizedBox(height: 18),
                const Text(
                  'Room B-204 is yours until 4:00 PM. Collect your items at the store counter.',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 15),
                ),
                const SizedBox(height: 18),
                const Tile(
                  children: [
                    Kv('Whiteboard markers', '2'),
                    Kv('A4 printouts', '20'),
                    Kv('Collect from', 'Store counter, ground floor'),
                  ],
                ),
                const SizedBox(height: 18),
                PrimaryButton(
                  'Done',
                  onPressed: () =>
                      Navigator.of(context).popUntil((route) => route.isFirst),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
