import 'package:flutter/material.dart';

import '../../models/room.dart';
import '../../theme/app_theme.dart';
import '../../widgets/studyhive_ui.dart';

/// M-15 "Checked in" — the success state returned by the room-booking API.
class CheckedInScreen extends StatelessWidget {
  final RoomCheckInResult? result;
  final bool previewEnabled;

  const CheckedInScreen({super.key, this.result, this.previewEnabled = false});

  @override
  Widget build(BuildContext context) {
    final room = result?.roomName ?? 'B-204';
    final until = result == null ? '4:00 PM' : _time(result!.endsAt.toLocal());

    if (!previewEnabled && result == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('StudyHive')),
        body: const PreviewUnavailable(
            message:
                'Check-in details will appear after a successful room check-in.'),
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
                        size: 26, color: AppColors.accent700))),
            const SizedBox(height: 18),
            Text('You are checked in',
                textAlign: TextAlign.center,
                style: headingStyle(
                    fontSize: 32, height: 1.12, letterSpacing: -0.5)),
            const SizedBox(height: 18),
            Text('Room $room is yours until $until.',
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 15)),
            if (previewEnabled) ...[
              const SizedBox(height: 18),
              const Tile(children: [
                Kv('Whiteboard markers', '2'),
                Kv('A4 printouts', '20'),
                Kv('Collect from', 'Store counter, ground floor'),
              ]),
            ],
            const SizedBox(height: 18),
            PrimaryButton('Done',
                onPressed: () =>
                    Navigator.of(context).popUntil((route) => route.isFirst)),
          ]),
    ))));
  }

  static String _time(DateTime value) {
    final hour = value.hour % 12 == 0 ? 12 : value.hour % 12;
    final minute = value.minute.toString().padLeft(2, '0');
    return '$hour:$minute ${value.hour < 12 ? 'AM' : 'PM'}';
  }
}
