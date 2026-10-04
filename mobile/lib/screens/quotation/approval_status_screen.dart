import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../utils/colombo_time.dart';
import '../../api/api_client.dart';
import '../../data/demo_seed.dart';
import '../../models/booking_request.dart';
import '../../models/workflow_status.dart';
import '../../state/booking_requests_provider.dart';
import '../../widgets/studyhive_ui.dart';
import 'quotation_view_screen.dart';

/// Not one of the 16 reference frames — it expands the "Waiting for librarian" tag on M-08.
///
/// With [requestId] it is live: GET /api/booking-requests/{id} gives the latest quotation and the
/// librarian's decision on it (outcome, comments, date), and GET /{id}/status gives the workflow,
/// whose VALIDATION_FAILED error message is the revision note for the student. Without one it
/// shows the seeded reference preview.
class ApprovalStatusScreen extends StatefulWidget {
  final String? requestId;

  const ApprovalStatusScreen({super.key, this.requestId});

  @override
  State<ApprovalStatusScreen> createState() => _ApprovalStatusScreenState();
}

class _ApprovalStatusScreenState extends State<ApprovalStatusScreen> {
  BookingRequest? _request;
  WorkflowStatus? _workflow;
  bool _loading = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    if (widget.requestId != null) _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    final api = context.read<BookingRequestsProvider>().api;
    try {
      final request = await api.getById(widget.requestId!);
      WorkflowStatus? workflow;
      try {
        workflow = await api.getStatus(widget.requestId!);
      } on ApiException catch (e) {
        if (e.status != 404) rethrow; // no workflow started yet
      }
      if (!mounted) return;
      setState(() {
        _request = request;
        _workflow = workflow;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error =
            e is ApiException
                ? e.toString()
                : 'Could not load the approval status.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final appBar = AppBar(title: const Text('Approval status'));

    if (widget.requestId == null) {
      if (!demoPreviewEnabled) {
        return Scaffold(
          appBar: appBar,
          body: const PreviewUnavailable(
            message: 'Approval details will appear when a quotation is ready.',
          ),
        );
      }
      return Scaffold(appBar: appBar, body: const _Preview());
    }

    if (_loading) {
      return Scaffold(
        appBar: appBar,
        body: const Center(child: CircularProgressIndicator()),
      );
    }
    if (_error != null) {
      return Scaffold(
        appBar: appBar,
        body: ScreenBody(
          children: [
            InlineError(_error!),
            SecondaryButton('Try again', onPressed: _load),
          ],
        ),
      );
    }
    return Scaffold(appBar: appBar, body: _live(_request!, _workflow));
  }

  Widget _live(BookingRequest request, WorkflowStatus? workflow) {
    final quotation = request.latestQuotation;
    final decision = request.latestDecision;
    final validationFailed = workflow?.errorCode == 'VALIDATION_FAILED';

    final (String tag, TagTone tone, String heading, String body) = switch ((
      decision?.decision,
      quotation?.status,
      workflow?.status,
    )) {
      ('Approved', _, _) => (
        'Approved',
        TagTone.accent,
        'Approved',
        'Your room is booked and your items are reserved.',
      ),
      ('Rejected', _, _) => (
        'Rejected',
        TagTone.outline,
        'Not approved',
        'The librarian did not approve this request.',
      ),
      ('RevisionRequested', _, _) => (
        'Changes requested',
        TagTone.outline,
        'Changes requested',
        'The librarian asked for a change before this can be booked.',
      ),
      (_, 'Proposed', _) => (
        'Waiting for librarian',
        TagTone.outline,
        'Waiting for a librarian',
        'Your room and items are proposed. A librarian will review the cost and availability.',
      ),
      _ when validationFailed => (
        'Needs changes',
        TagTone.outline,
        'Your request needs changes',
        'The proposal did not pass the checks, so it was not sent to a librarian.',
      ),
      (_, _, 'Failed') => (
        'Failed',
        TagTone.outline,
        'Something went wrong',
        'The assistant could not finish this request.',
      ),
      _ => (
        request.status,
        TagTone.neutral,
        'Still being prepared',
        'Your request is still being worked on.',
      ),
    };

    return ScreenBody(
      children: [
        Tile(
          accented: true,
          children: [
            Align(
              alignment: Alignment.centerLeft,
              child: ShTag(tag, tone: tone),
            ),
            Heading(heading, fontSize: 20),
            Text(body, style: const TextStyle(fontSize: 14)),
            if (decision != null)
              FNote('Decided ${_stamp(decision.decidedAt)}'),
          ],
        ),
        if (decision?.comments != null)
          Tile(
            children: [
              const Lbl("Librarian's comment"),
              Text(decision!.comments!),
            ],
          ),
        // The Validation agent's revision note: why the proposal was sent back automatically.
        if (validationFailed && workflow?.errorMessage != null)
          Tile(
            tinted: true,
            children: [
              const Lbl('What to change'),
              Text(workflow!.errorMessage!),
            ],
          ),
        if (!validationFailed &&
            workflow?.status == 'Failed' &&
            workflow?.errorMessage != null)
          InlineError(workflow!.errorMessage!),
        if (quotation != null)
          PrimaryButton(
            'View cost breakdown',
            // C-20: swap with Cost breakdown instead of stacking the two screens on each other.
            onPressed:
                () => Navigator.of(context).pushReplacement(
                  MaterialPageRoute(
                    builder: (_) => QuotationViewScreen(requestId: request.id),
                  ),
                ),
          ),
        if (quotation?.status == 'Proposed')
          const FNote('You will get an email as soon as they decide.'),
      ],
    );
  }
}

class _Preview extends StatelessWidget {
  const _Preview();

  @override
  Widget build(BuildContext context) => const ScreenBody(
    children: [
      DemoPreviewBanner(),
      Tile(
        accented: true,
        children: [
          Align(
            alignment: Alignment.centerLeft,
            child: ShTag('Waiting for librarian', tone: TagTone.outline),
          ),
          Heading('Waiting for a librarian', fontSize: 20),
          Text(
            'Your room and items are proposed. A librarian will review the cost and availability.',
            style: TextStyle(fontSize: 14),
          ),
        ],
      ),
      FNote('You will get an email as soon as they decide.'),
    ],
  );
}

/// CW-11: "Mon 6 Oct, 14:05" in Colombo time, whatever the phone's own zone.
String _stamp(String iso) {
  final parsed = DateTime.tryParse(iso);
  if (parsed == null) return '';
  return '${colomboDay(parsed)}, ${colomboHhmm(parsed)}';
}
