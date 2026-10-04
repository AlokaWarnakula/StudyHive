import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../models/booking_request.dart';
import '../models/workflow_status.dart';
import '../state/booking_requests_provider.dart';
import '../utils/colombo_time.dart';
import '../widgets/studyhive_ui.dart';
import 'create_request_screen.dart';
import 'quotation/approval_status_screen.dart';
import 'quotation/quotation_view_screen.dart';
import 'rooms/qr_check_in_screen.dart';
import 'workflow_progress_screen.dart';

const _activeWorkflowStatuses = {'Started', 'InProgress'};
const _waitingStatuses = {'Submitted', 'Processing', 'PendingApproval'};

/// M-13 "Booking detail" — GET /api/booking-requests/{id} with the full status
/// timeline from workflow_executions. Polls /status every 3s while the workflow
/// is still running, same cadence as the sequence diagram in DOCS §11.
///
/// AUDIT C-06 / C-14: once approved it shows the booked room and slot (Colombo time) and whether
/// the student checked in; check-in and cancel disappear after check-in. C-07: Cancel asks first and
/// is offered only while the API allows it. C-02: a revision shows the librarian's comment and
/// "Edit and resend". C-11: a Draft can be sent or deleted from here.
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
  bool _busy = false;

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

  Future<void> _push(Widget screen) async {
    await Navigator.of(context).push(MaterialPageRoute(builder: (_) => screen));
    if (mounted) await _load(); // C-08: back from any screen, show what changed there
  }

  void _toast(String message) => ScaffoldMessenger.of(context)
      .showSnackBar(SnackBar(content: Text(message)));

  Future<void> _cancel(BookingRequest request) async {
    final isDraft = request.status == 'Draft';
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(isDraft ? 'Delete this draft?' : 'Cancel this booking?'),
        content: Text(
          request.status == 'Approved'
              ? 'The room time is released for others and any items set aside go back to the store.'
              : isDraft
                  ? 'It was never sent, so nobody has seen it yet.'
                  : 'The librarian will no longer see this request.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(isDraft ? 'Keep it' : 'Keep booking'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(isDraft ? 'Delete draft' : 'Cancel booking'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    setState(() => _busy = true);
    try {
      await context.read<BookingRequestsProvider>().cancel(widget.requestId);
      if (mounted) Navigator.of(context).pop();
    } on ApiException catch (e) {
      if (mounted) {
        _toast(e.toString());
        await _load(); // e.g. the booking started meanwhile: show the real state
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _sendDraft(BookingRequest request) async {
    setState(() => _busy = true);
    try {
      await context.read<BookingRequestsProvider>().submit(request.id);
      if (!mounted) return;
      Navigator.of(context).pushReplacement(MaterialPageRoute(
          builder: (_) => WorkflowProgressScreen(requestId: request.id)));
    } on ApiException catch (e) {
      if (mounted) _toast(e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// "What happened": the request itself, one line per agent step, the librarian's decision,
  /// then the check-in.
  List<TimelineStep> _timeline(BookingRequest request) {
    final steps = <TimelineStep>[
      TimelineStep(
        'Request sent',
        detail: _stamp(request.createdAt),
        state: TlState.done,
      ),
    ];

    for (final step in _workflow?.steps ?? const <WorkflowStepLog>[]) {
      steps.add(
        TimelineStep(
          'Step ${step.stepNumber} — ${step.agentName}',
          detail: step.errorMessage ?? step.validationResult,
          state: step.errorMessage != null ? TlState.current : TlState.done,
        ),
      );
    }

    final decision = request.latestDecision;
    if (decision != null && decision.decision == 'Approved') {
      steps.add(TimelineStep('Approved by librarian',
          detail: _stamp(decision.decidedAt), state: TlState.done));
    }

    final checkedInAt = request.checkedInAt;
    if (checkedInAt != null) {
      steps.add(TimelineStep('Checked in',
          detail: '${colomboDay(checkedInAt)} · ${colomboHhmm(checkedInAt)}',
          state: TlState.done));
    } else if (request.canCheckIn) {
      steps.add(const TimelineStep(
        'Check in at the room',
        detail: 'Opens 15 min before',
        state: TlState.waiting,
      ));
    } else if (request.status == 'RevisionRequested') {
      steps.add(const TimelineStep('Changes asked for',
          detail: 'Edit the request and send it again', state: TlState.current));
    } else if (_waitingStatuses.contains(request.status)) {
      steps.add(TimelineStep(
        'Waiting for the librarian',
        detail: 'Status: ${request.status}',
        state: TlState.current,
      ));
    }

    return steps;
  }

  @override
  Widget build(BuildContext context) {
    final request = _request;
    return Scaffold(
      appBar: AppBar(
        title: Text(
          request == null ? 'Booking detail' : request.objective,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
        ),
      ),
      body:
          _loading
              ? const Center(child: CircularProgressIndicator())
              : _error != null
              ? Padding(
                padding: const EdgeInsets.all(16),
                child: InlineError(_error!),
              )
              : _buildBody(request!),
    );
  }

  Widget _buildBody(BookingRequest request) {
    final slot = request.slot;
    final checkedInAt = request.checkedInAt;
    final decision = request.latestDecision;
    return ScreenBody(
      children: [
        Tile(
          accented: true,
          children: [
            Align(
              alignment: Alignment.centerLeft,
              child: ShTag.forStatus(
                  checkedInAt != null ? 'Checked in' : request.status),
            ),
            Big(slot != null
                ? colomboSlot(slot.startsAt, slot.endsAt)
                : '${request.preferredDateFrom} · ${_hhmm(request.preferredTimeFrom)} – ${_hhmm(request.preferredTimeTo)}'),
            if (slot != null) Text(slot.roomName),
            FNote(
              '${request.groupSize} people · Rs. ${request.budget.toStringAsFixed(0)} budget · ${request.sessionsRequired} × ${request.sessionDurationMinutes} min',
            ),
          ],
        ),
        if (checkedInAt != null)
          Tile(children: [
            Text('Checked in at ${colomboHhmm(checkedInAt)}',
                style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
            const FNote('Enjoy the room. Nothing else to do here.'),
          ]),
        if (request.status == 'RevisionRequested')
          Tile(children: [
            const Lbl('The librarian asked for a change'),
            Text(decision?.comments?.trim().isNotEmpty == true
                ? decision!.comments!
                : 'No comment was left.'),
          ]),
        const Lbl('What happened'),
        Timeline(steps: _timeline(request)),
        if (request.notes != null)
          Tile(children: [const Lbl('Notes'), Text(request.notes!)]),
        if (request.status == 'RevisionRequested')
          PrimaryButton(
            'Edit and resend',
            onPressed: _busy
                ? null
                : () => _push(CreateRequestScreen(editing: request)),
          ),
        if (request.status == 'Draft') ...[
          PrimaryButton(_busy ? 'Sending…' : 'Send',
              onPressed: _busy ? null : () => _sendDraft(request)),
          SecondaryButton('Edit draft',
              onPressed: _busy
                  ? null
                  : () => _push(CreateRequestScreen(editing: request))),
        ],
        if (request.latestQuotation != null)
          PrimaryButton(
            'View cost breakdown',
            onPressed: () => _push(QuotationViewScreen(requestId: request.id)),
          ),
        if (request.latestQuotation != null || _workflow != null)
          SecondaryButton(
            'Approval status',
            onPressed: () => _push(ApprovalStatusScreen(requestId: request.id)),
          ),
        if (request.canCheckIn)
          PrimaryButton(
            'Check in with QR',
            icon: Icons.qr_code_2,
            onPressed: () => _push(QrCheckInScreen(bookingId: request.id)),
          ),
        if (request.canCancel)
          SecondaryButton(
            _busy
                ? 'Please wait…'
                : request.status == 'Draft'
                    ? 'Delete draft'
                    : 'Cancel booking',
            onPressed: _busy ? null : () => _cancel(request),
          ),
      ],
    );
  }
}

String _hhmm(String time) => time.length >= 5 ? time.substring(0, 5) : time;

String _stamp(String isoTimestamp) {
  final parsed = DateTime.tryParse(isoTimestamp);
  if (parsed == null) return isoTimestamp;
  return '${colomboDay(parsed)} · ${colomboHhmm(parsed)}';
}
