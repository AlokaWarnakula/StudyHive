import 'package:flutter/material.dart';

import '../../data/demo_seed.dart';
import '../../theme/app_theme.dart';
import '../../widgets/studyhive_ui.dart';
import 'checked_in_screen.dart';

/// M-14 "QR check-in" — a device feature (camera) posting to
/// /api/room-bookings/{id}/check-in. The reference draws this frame full-bleed
/// dark with no status bar or app bar of its own.
class QrCheckInScreen extends StatelessWidget {
  final String? bookingId;

  const QrCheckInScreen({super.key, this.bookingId});

  @override
  Widget build(BuildContext context) {
    if (!demoPreviewEnabled) {
      return Scaffold(
        appBar: AppBar(title: const Text('Scan the room QR')),
        body: const PreviewUnavailable(
          message:
              'QR check-in will be enabled when scanner support is connected.',
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.neutral900,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  IconButton(
                    onPressed: () => Navigator.of(context).maybePop(),
                    tooltip: 'Close',
                    icon: const Icon(Icons.close,
                        size: 22, color: AppColors.neutral100),
                  ),
                  const Text('Scan the room QR',
                      style: TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                          color: AppColors.neutral100)),
                  const SizedBox(width: 40),
                ],
              ),
              // The camera preview stands in for the real scanner; tapping it
              // walks the preview build through to M-15. It is square, but only
              // as large as the remaining height allows.
              Expanded(
                child: Center(
                  child: AspectRatio(
                    aspectRatio: 1,
                    child: InkWell(
                      onTap: () => Navigator.of(context).pushReplacement(
                        MaterialPageRoute(
                            builder: (_) => const CheckedInScreen()),
                      ),
                      child: Container(
                        decoration: BoxDecoration(
                          border:
                              Border.all(color: AppColors.accent300, width: 2),
                        ),
                        alignment: Alignment.center,
                        child: const Text(
                          'CAMERA VIEW — TAP TO PREVIEW A SCAN',
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            fontFamily: 'monospace',
                            fontSize: 11,
                            letterSpacing: 0.6,
                            color: AppColors.neutral400,
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
              Column(
                children: [
                  const Text('Point at the sticker on the door',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                          fontSize: 17,
                          fontWeight: FontWeight.w600,
                          color: AppColors.neutral100)),
                  const SizedBox(height: 6),
                  Text('Booking B-204 · today 2:00 PM',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                          fontSize: 13,
                          color:
                              AppColors.neutral100.withValues(alpha: 0.7))),
                ],
              ),
              SecondaryButton(
                'Enter room code instead',
                onPressed: () {},
                foreground: AppColors.neutral100,
                borderColor: AppColors.neutral600,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
