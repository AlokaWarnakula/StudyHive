import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:provider/provider.dart';

import '../../state/rooms_provider.dart';
import '../../theme/app_theme.dart';
import '../../widgets/studyhive_ui.dart';
import 'checked_in_screen.dart';

/// M-14 "QR check-in" — submits the code printed on the assigned room's QR sticker.
class QrCheckInScreen extends StatefulWidget {
  final String? bookingId;
  final bool previewEnabled;

  const QrCheckInScreen(
      {super.key, this.bookingId, this.previewEnabled = false});

  @override
  State<QrCheckInScreen> createState() => _QrCheckInScreenState();
}

class _QrCheckInScreenState extends State<QrCheckInScreen> {
  late final MobileScannerController _scanner = MobileScannerController(
    formats: const [BarcodeFormat.qrCode],
    detectionSpeed: DetectionSpeed.noDuplicates,
  );
  bool _submitting = false;
  String? _error;

  bool get _supportsScanner =>
      kIsWeb ||
      defaultTargetPlatform == TargetPlatform.android ||
      defaultTargetPlatform == TargetPlatform.iOS;

  @override
  void dispose() {
    unawaited(_scanner.dispose());
    super.dispose();
  }

  Future<void> _handleCapture(BarcodeCapture capture) async {
    if (_submitting) return;
    String? code;
    for (final barcode in capture.barcodes) {
      if (barcode.rawValue case final value? when value.trim().isNotEmpty) {
        code = value.trim();
        break;
      }
    }
    if (code == null) return;

    await _scanner.stop();
    final succeeded = await _submit(code);
    if (!succeeded && mounted) await _scanner.start();
  }

  Future<void> _enterCode() async {
    if (widget.previewEnabled) {
      _showPreviewSuccess();
      return;
    }

    final controller = TextEditingController();
    final code = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Enter room code'),
        content: TextField(
          controller: controller,
          autofocus: true,
          decoration: const InputDecoration(labelText: 'Room QR code'),
          onSubmitted: (value) => Navigator.of(dialogContext).pop(value),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Cancel')),
          FilledButton(
              onPressed: () => Navigator.of(dialogContext).pop(controller.text),
              child: const Text('Check in')),
        ],
      ),
    );
    controller.dispose();
    if (code == null || code.trim().isEmpty) return;
    await _submit(code.trim());
  }

  Future<bool> _submit(String code) async {
    final bookingId = widget.bookingId;
    if (bookingId == null || bookingId.isEmpty) {
      setState(
          () => _error = 'This booking does not have a check-in reference.');
      return false;
    }
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      final result =
          await context.read<RoomsProvider>().api.checkIn(bookingId, code);
      if (!mounted) return true;
      Navigator.of(context).pushReplacement(MaterialPageRoute(
        builder: (_) => CheckedInScreen(result: result),
      ));
      return true;
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
      return false;
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  void _showPreviewSuccess() {
    Navigator.of(context).pushReplacement(MaterialPageRoute(
      builder: (_) => const CheckedInScreen(previewEnabled: true),
    ));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.neutral900,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
                IconButton(
                    onPressed: () => Navigator.of(context).maybePop(),
                    tooltip: 'Close',
                    icon: const Icon(Icons.close,
                        size: 22, color: AppColors.neutral100)),
                const Text('Scan the room QR',
                    style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppColors.neutral100)),
                const SizedBox(width: 40),
              ]),
              Expanded(
                  child: Center(
                      child: AspectRatio(
                          aspectRatio: 1,
                          child: Container(
                            decoration: BoxDecoration(
                                border: Border.all(
                                    color: AppColors.accent300, width: 2)),
                            clipBehavior: Clip.hardEdge,
                            child: widget.previewEnabled
                                ? InkWell(
                                    onTap: _showPreviewSuccess,
                                    child: const Center(
                                        child: Text(
                                            'CAMERA VIEW — TAP TO PREVIEW A SCAN',
                                            textAlign: TextAlign.center,
                                            style: TextStyle(
                                                fontFamily: 'monospace',
                                                fontSize: 11,
                                                letterSpacing: 0.6,
                                                color: AppColors.neutral400))))
                                : _supportsScanner
                                    ? MobileScanner(
                                        controller: _scanner,
                                        onDetect: _handleCapture,
                                        errorBuilder: (context, error) =>
                                            Center(
                                                child: Padding(
                                          padding: const EdgeInsets.all(16),
                                          child: Text(
                                              'Camera unavailable: ${error.errorCode.name}',
                                              textAlign: TextAlign.center,
                                              style: const TextStyle(
                                                  color: AppColors.neutral100)),
                                        )),
                                      )
                                    : const Center(
                                        child: Padding(
                                        padding: EdgeInsets.all(16),
                                        child: Text(
                                            'Camera scanning is available on Android, iOS and web. Use room code entry on this device.',
                                            textAlign: TextAlign.center,
                                            style: TextStyle(
                                                color: AppColors.neutral400)),
                                      )),
                          )))),
              Column(children: [
                const Text('Point at the sticker on the door',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                        fontSize: 17,
                        fontWeight: FontWeight.w600,
                        color: AppColors.neutral100)),
                const SizedBox(height: 6),
                Text(
                    widget.previewEnabled
                        ? 'Booking B-204 · today 2:00 PM'
                        : 'Use the room code below to check in',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                        fontSize: 13,
                        color: AppColors.neutral100.withValues(alpha: 0.7))),
                if (_error != null) ...[
                  const SizedBox(height: 10),
                  Text(_error!,
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: Colors.redAccent)),
                ],
              ]),
              SecondaryButton(
                  _submitting ? 'Checking in…' : 'Enter room code instead',
                  onPressed: _submitting ? null : _enterCode,
                  foreground: AppColors.neutral100,
                  borderColor: AppColors.neutral600),
            ],
          ),
        ),
      ),
    );
  }
}
