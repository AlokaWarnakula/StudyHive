import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../models/booking_request.dart';
import '../models/workflow_status.dart';
import '../state/booking_requests_provider.dart';
import '../widgets/studyhive_ui.dart';
import 'quotation/quotation_view_screen.dart';
import 'rooms/qr_check_in_screen.dart';

const _activeWorkflowStatuses = {'Started', 'InProgress'};
const _cancellableStatuses = {
  'Draft',
  'Submitted',
  'Processing',
  'PendingApproval',
  'RevisionRequested'
};

/// M-13 "Booking detail" — GET /api/booking-requests/{id} with the full status
/// timeline from workflow_executions. Polls /status every 3s while the workflow
/// is still running, same cadence as the sequence diagram in DOCS §11.
class BookingDetailScreen extends StatefulWidget {
  final String requestId;
  const BookingDetailScreen({super.key, required this.requestId});

  @override
  State<BookingDetailScreen> createState() => _BookingDetailScreenState();
}

class _BookingDetailScreenState extends State<BookingDetailScreen> {
  BookingRequest? _request;
  WorkflowStatus? _workflow;
  String? _error;
  bool _loading = true;
  bool _cancelling = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final provider = context.read<BookingRequestsProvider>();
    try {
      final request = await provider.api.getById(widget.requestId);
      WorkflowStatus? workflow;
      try {
        workflow = await provider.api.getStatus(widget.requestId);
      } on ApiException catch (e) {
        if (e.status != 404) rethrow;
      }

      if (!mounted) return;
      setState(() {
        _request = request;
        _workflow = workflow;
        _error = null;
        _loading = false;
      });

      if (workflow != null &&
          _activeWorkflowStatuses.contains(workflow.status)) {
        Future.delayed(const Duration(seconds: 3), () {
          if (mounted) _load();
        });
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error =
            e is ApiException ? e.toString() : 'Failed to load this request.';
        _loading = false;
      });
    }
  }

  Future<void> _cancel() async {
    setState(() => _cancelling = true);
    try {
      await context.read<BookingRequestsProvider>().cancel(widget.requestId);
      if (mounted) Navigator.of(context).pop();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(e.toString())));
      }
    } finally {
      if (mounted) setState(() => _cancelling = false);
    }
  }

  /// "What happened": the request itself, then one line per agent step, then the
  /// check-in the student is waiting for.
  List<TimelineStep> _timeline(BookingRequest request) {
    final steps = <TimelineStep>[
      TimelineStep('Request sent',
          detail: _stamp(request.createdAt), state: TlState.done),
    ];

    for (final step in _workflow?.steps ?? const <WorkflowStepLog>[]) {
      steps.add(TimelineStep(
        'Step ${step.stepNumber} — ${step.agentName}',
        detail: step.errorMessage ?? step.validationResult,
        state: step.errorMessage != null ? TlState.current : TlState.done,
      ));
    }

    if (request.status == 'Approved') {
      steps.add(const TimelineStep('Check in at the room',
          detail: 'Opens 15 min before', state: TlState.waiting));
    } else if (_cancellableStatuses.contains(request.status)) {
      steps.add(TimelineStep('Waiting for the librarian',
          detail: 'Status: ${request.status}', state: TlState.current));
    }

    return steps;
  }

  @override
  Widget build(BuildContext context) {
    final request = _request;
    return Scaffold(
      appBar: AppBar(
        title: Text(request == null ? 'Booking detail' : request.objective,
            maxLines: 1, overflow: TextOverflow.ellipsis),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Padding(
                  padding: const EdgeInsets.all(16),
                  child: InlineError(_error!))
              : _buildBody(request!),
    );
  }

  Widget _buildBody(BookingRequest request) {
    return ScreenBody(
      children: [
        Tile(
          accented: true,
          children: [
            Align(
                alignment: Alignment.centerLeft,
                child: ShTag.forStatus(request.status)),
            Big(
                '${request.preferredDateFrom} · ${_hhmm(request.preferredTimeFrom)} – ${_hhmm(request.preferredTimeTo)}'),
            FNote(
                '${request.groupSize} people · Rs. ${request.budget.toStringAsFixed(0)} budget · ${request.sessionsRequired} × ${request.sessionDurationMinutes} min'),
          ],
        ),
        const Lbl('What happened'),
        Timeline(steps: _timeline(request)),
        if (request.notes != null)
          Tile(children: [
            const Lbl('Notes'),
            Text(request.notes!),
          ]),
        if (request.status == 'PendingApproval')
          PrimaryButton(
            'View cost breakdown',
            onPressed: () => Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const QuotationViewScreen())),
          ),
        if (request.status == 'Approved')
          PrimaryButton(
            'Check in with QR',
            icon: Icons.qr_code_2,
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(
                builder: (_) => QrCheckInScreen(bookingId: request.id))),
          ),
        if (_cancellableStatuses.contains(request.status) ||
            request.status == 'Approved')
          SecondaryButton(
            _cancelling ? 'Cancelling…' : 'Cancel booking',
            onPressed: _cancelling ? null : _cancel,
          ),
      ],
    );
  }
}

String _hhmm(String time) => time.length >= 5 ? time.substring(0, 5) : time;

String _stamp(String isoTimestamp) {
  final parsed = DateTime.tryParse(isoTimestamp);
  if (parsed == null) return isoTimestamp;
  final local = parsed.toLocal();
  final hour = local.hour % 12 == 0 ? 12 : local.hour % 12;
  final minute = local.minute.toString().padLeft(2, '0');
  return '$hour:$minute ${local.hour < 12 ? 'AM' : 'PM'}';
}
