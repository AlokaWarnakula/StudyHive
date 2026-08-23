import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../models/workflow_status.dart';
import '../state/booking_requests_provider.dart';
import '../widgets/studyhive_ui.dart';
import 'quotation/quotation_view_screen.dart';

/// M-07 "Finding your room" — polls GET /api/booking-requests/{id}/status and
/// renders the four agents in order as the reference timeline.
class WorkflowProgressScreen extends StatefulWidget {
  final String requestId;

  const WorkflowProgressScreen({super.key, required this.requestId});

  @override
  State<WorkflowProgressScreen> createState() => _WorkflowProgressScreenState();
}

class _WorkflowProgressScreenState extends State<WorkflowProgressScreen> {
  /// The five lines the reference prints, in order.
  static const _stepTitles = [
    'Checked your eligibility',
    'Found free rooms',
    'Reserving your items',
    'Working out the cost',
    'Sending to librarian',
  ];

  WorkflowStatus? _workflow;
  String? _error;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _refresh() async {
    try {
      final workflow = await context
          .read<BookingRequestsProvider>()
          .api
          .getStatus(widget.requestId);
      if (!mounted) return;
      setState(() {
        _workflow = workflow;
        _error = null;
      });
      if ({'Started', 'InProgress'}.contains(workflow.status)) {
        _timer?.cancel();
        _timer = Timer(const Duration(seconds: 3), _refresh);
      }
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _error =
          error.status == 404 ? 'The workflow is starting…' : error.toString());
      if (error.status == 404) {
        _timer?.cancel();
        _timer = Timer(const Duration(seconds: 2), _refresh);
      }
    } catch (_) {
      if (mounted) setState(() => _error = 'Could not refresh progress.');
    }
  }

  List<TimelineStep> _steps() {
    final logs = _workflow?.steps ?? const <WorkflowStepLog>[];
    final sentToLibrarian = _workflow?.status == 'PendingApproval';

    return [
      for (var i = 0; i < _stepTitles.length; i++)
        () {
          // The final line is driven by the workflow status; the first four by
          // how many agent steps have been logged.
          final done = i == _stepTitles.length - 1
              ? sentToLibrarian
              : logs.length > i;
          final current = !done &&
              (i == _stepTitles.length - 1
                  ? logs.length >= _stepTitles.length - 1
                  : logs.length == i);
          final log = i < logs.length ? logs[i] : null;
          return TimelineStep(
            _stepTitles[i],
            detail: log?.errorMessage ??
                (done
                    ? log?.agentName
                    : current
                        ? 'In progress'
                        : 'Waiting'),
            state: done
                ? TlState.done
                : current
                    ? TlState.current
                    : TlState.waiting,
          );
        }(),
    ];
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        automaticallyImplyLeading: false,
        title: const Text('Working on it'),
      ),
      body: ScreenBody(
        children: [
          const Ph(label: 'progress illustration', height: 130),
          const Heading('Finding a free room for you'),
          const Text(
            'This usually takes under a minute. You can close the app — we will notify you.',
            style: TextStyle(fontSize: 14),
          ),
          Timeline(steps: _steps()),
          if (_error != null) ...[
            InlineError(_error!),
            SecondaryButton('Try again', onPressed: _refresh),
          ],
          if (_workflow?.status == 'PendingApproval')
            PrimaryButton(
              'View cost breakdown',
              onPressed: () => Navigator.of(context).push(MaterialPageRoute(
                  builder: (_) => const QuotationViewScreen())),
            ),
          SecondaryButton('Back to home',
              onPressed: () => Navigator.of(context).pop()),
        ],
      ),
    );
  }
}
