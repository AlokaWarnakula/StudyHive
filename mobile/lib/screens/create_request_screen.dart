import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/booking_requests_api.dart';
import '../models/booking_request.dart';
import '../state/booking_requests_provider.dart';
import '../state/consumables_provider.dart';
import '../theme/app_theme.dart';
import '../widgets/studyhive_ui.dart';
import 'consumables/select_consumables_screen.dart';
import 'workflow_progress_screen.dart';

String _isoDate(DateTime value) =>
    '${value.year.toString().padLeft(4, '0')}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';

String _isoTime(TimeOfDay value) =>
    '${value.hour.toString().padLeft(2, '0')}:${value.minute.toString().padLeft(2, '0')}:00';

const _weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const _months = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
];

/// "Mon, 24 Aug 2026" — the format the reference prints in the Date field.
String _longDate(DateTime value) =>
    '${_weekdays[value.weekday - 1]}, ${value.day} ${_months[value.month - 1]} ${value.year}';

/// M-04 / M-05 / M-06 — the three-step booking flow. The reference asks for a
/// single date (from and to are the same day) plus a start and end time.
///
/// Step 2's consumables come from the live catalogue through [ConsumablesProvider]; its
/// selection (shared with the full-screen [SelectConsumablesScreen]) is sent as the request's
/// `items`. Without a registered provider the step says item selection is unavailable.
///
/// AUDIT C-09: "Book this room" and "Use a slot" pass the room and time they came from, so the form
/// starts there and says which room was preferred (advisory: the librarian confirms the final room;
/// the preference travels in the request notes). AUDIT C-02: with [editing], the form opens a
/// RevisionRequested (or Draft) request pre-filled and sends it back with PUT then submit.
/// AUDIT C-11: a submit that fails keeps the saved draft, and sending again retries that draft.
class CreateRequestScreen extends StatefulWidget {
  final DateTime? initialDate;
  final TimeOfDay? initialFrom;
  final TimeOfDay? initialTo;
  final String? roomName;
  final BookingRequest? editing;

  const CreateRequestScreen({
    super.key,
    this.initialDate,
    this.initialFrom,
    this.initialTo,
    this.roomName,
    this.editing,
  });

  @override
  State<CreateRequestScreen> createState() => _CreateRequestScreenState();
}

class _CreateRequestScreenState extends State<CreateRequestScreen> {
  final _formKey = GlobalKey<FormState>();
  final _objective = TextEditingController();
  final _budget = TextEditingController(text: '1000');
  int _step = 0;
  int _people = 4;
  DateTime _date = DateTime.now().add(const Duration(days: 3));
  TimeOfDay _timeFrom = const TimeOfDay(hour: 14, minute: 0);
  TimeOfDay _timeTo = const TimeOfDay(hour: 16, minute: 0);
  String? _error;
  bool _submitting = false;

  /// The saved draft this form sends (C-11): set after the first save, reused on every retry.
  String? _draftId;

  @override
  void initState() {
    super.initState();
    final editing = widget.editing;
    if (editing != null) {
      _draftId = editing.id;
      _objective.text = editing.objective;
      _budget.text = editing.budget.toStringAsFixed(0);
      _people = editing.groupSize;
      _date = DateTime.tryParse(editing.preferredDateFrom) ?? _date;
      _timeFrom = _parseTime(editing.preferredTimeFrom) ?? _timeFrom;
      _timeTo = _parseTime(editing.preferredTimeTo) ?? _timeTo;
    } else {
      _date = widget.initialDate ?? _date;
      _timeFrom = widget.initialFrom ?? _timeFrom;
      _timeTo = widget.initialTo ?? _timeTo;
    }
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      final consumables = _consumables;
      // A new request starts with no items, whatever an abandoned earlier one picked; an edited
      // one starts with its own.
      consumables?.clearSelection();
      for (final item in editing?.items ?? const <BookingRequestItem>[]) {
        consumables?.setQuantity(item.consumableId, item.quantity);
      }
    });
  }

  static TimeOfDay? _parseTime(String value) {
    final parts = value.split(':');
    if (parts.length < 2) return null;
    final hour = int.tryParse(parts[0]);
    final minute = int.tryParse(parts[1]);
    return hour == null || minute == null ? null : TimeOfDay(hour: hour, minute: minute);
  }

  /// The preference the librarian sees (C-09): advisory, carried in the notes.
  String? get _notes {
    final editingNotes = widget.editing?.notes;
    if (editingNotes != null) return editingNotes;
    final room = widget.roomName;
    return room == null ? null : 'Preferred room: $room';
  }

  @override
  void dispose() {
    _objective.dispose();
    _budget.dispose();
    super.dispose();
  }

  /// Null when no provider is registered above this screen (e.g. a design-frame test).
  ConsumablesProvider? get _consumables =>
      Provider.of<ConsumablesProvider?>(context, listen: false);

  Future<void> _pickDate() async {
    final selected = await showDatePicker(
      context: context,
      initialDate: _date,
      firstDate: DateUtils.dateOnly(DateTime.now()),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );
    if (selected != null) setState(() => _date = selected);
  }

  Future<void> _pickTime(bool from) async {
    final selected = await showTimePicker(
        context: context, initialTime: from ? _timeFrom : _timeTo);
    if (selected == null) return;
    setState(() => from ? _timeFrom = selected : _timeTo = selected);
  }

  void _next() {
    if (_step == 0 && !_formKey.currentState!.validate()) return;
    if (_step == 0) {
      final consumables = _consumables;
      if (consumables != null && consumables.items.isEmpty && !consumables.loading) {
        consumables.refresh();
      }
    }
    setState(() => _step += 1);
  }

  Future<void> _openPicker() async {
    await Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => const SelectConsumablesScreen()));
    // The picker may have left the list filtered by its search; step 2 shows the whole catalogue.
    if (mounted) await _consumables?.refresh();
  }

  void _back() {
    if (_step == 0) {
      Navigator.of(context).pop();
    } else {
      setState(() => _step -= 1);
    }
  }

  Future<void> _submit() async {
    setState(() {
      _error = null;
      _submitting = true;
    });
    final consumables = _consumables;
    final provider = context.read<BookingRequestsProvider>();
    final fields = BookingRequestFields(
      objective: _objective.text.trim(),
      groupSize: _people,
      preferredDateFrom: _isoDate(_date),
      preferredDateTo: _isoDate(_date),
      preferredTimeFrom: _isoTime(_timeFrom),
      preferredTimeTo: _isoTime(_timeTo),
      sessionsRequired: 1,
      sessionDurationMinutes: _durationMinutes,
      budget: double.parse(_budget.text),
      items: consumables?.selectedItems ?? const [],
      notes: _notes,
    );
    try {
      // Save first (create once, then update the same draft), then send it. If the send fails the
      // draft is kept, so "Send request" again retries it rather than creating a second one.
      final draftId = _draftId;
      final saved = draftId == null
          ? await provider.createDraft(fields)
          : await provider.updateDraft(draftId, fields);
      _draftId = saved.id;
      await provider.submit(saved.id);
      if (!mounted) return;
      consumables?.clearSelection();
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(
            builder: (_) => WorkflowProgressScreen(requestId: saved.id)),
      );
    } on ApiException catch (e) {
      setState(() => _error = e.toString());
    } catch (_) {
      setState(
          () => _error = 'The request could not be sent. Please try again.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  int get _durationMinutes {
    final start = _timeFrom.hour * 60 + _timeFrom.minute;
    final end = _timeTo.hour * 60 + _timeTo.minute;
    return (end - start).clamp(30, 480);
  }


  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: IconButton(
          onPressed: _back,
          icon: Icon(_step == 0 ? Icons.close : Icons.chevron_left, size: 24),
          tooltip: _step == 0 ? 'Close' : 'Back',
        ),
        title: Text(switch (_step) {
          1 => 'Add items',
          2 => 'Review request',
          _ => widget.editing != null ? 'Edit request' : 'Book a room',
        }),
      ),
      body: AnimatedSwitcher(
        duration: const Duration(milliseconds: 180),
        child: switch (_step) {
          0 => _buildWhen(),
          1 => _buildItems(),
          _ => _buildReview(),
        },
      ),
    );
  }

  /// M-04 "Book a room — step 1": objective, group size, date and time.
  Widget _buildWhen() {
    return Form(
      key: _formKey,
      child: ScreenBody(
        key: const ValueKey('request-step-1'),
        children: [
          const StepperBar(step: 0),
          const Lbl('Step 1 of 3 · What and when'),
          if (widget.editing != null)
            const FNote('Change what the librarian asked for, then send it again.'),
          if (widget.roomName != null)
            Tile(children: [
              Kv('Preferred room', widget.roomName!),
              const FNote('The librarian confirms the final room.'),
            ]),
          ShTextField(
            label: 'What do you need the room for?',
            controller: _objective,
            maxLines: 3,
            validator: (value) => value == null || value.trim().isEmpty
                ? 'Tell us what the room is for'
                : null,
          ),
          Field(
            label: 'How many people?',
            child: Align(
              alignment: Alignment.centerLeft,
              child: CounterControl(
                value: _people,
                min: 1,
                max: 50,
                onChanged: (value) => setState(() => _people = value),
              ),
            ),
          ),
          Field(
            label: 'Date',
            child: _PickerBox(
              value: _longDate(_date),
              onTap: _pickDate,
            ),
          ),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Field(
                  label: 'From',
                  child: _PickerBox(
                      value: _timeFrom.format(context),
                      onTap: () => _pickTime(true)),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Field(
                  label: 'To',
                  child: _PickerBox(
                      value: _timeTo.format(context),
                      onTap: () => _pickTime(false)),
                ),
              ),
            ],
          ),
          ShTextField(
            label: 'Budget (Rs.)',
            controller: _budget,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            validator: (value) {
              final number = double.tryParse(value ?? '');
              return number == null || number <= 0
                  ? 'Enter a budget greater than 0'
                  : null;
            },
          ),
          PrimaryButton('Next: add items', onPressed: _next),
        ],
      ),
    );
  }

  /// M-05 "Book a room — step 2": the optional consumables picker, over the live catalogue.
  Widget _buildItems() {
    final consumables = context.watch<ConsumablesProvider?>();
    final total = consumables?.selectionTotal ?? 0;

    return ScreenBody(
      key: const ValueKey('request-step-2'),
      children: [
        const StepperBar(step: 1),
        const Lbl('Step 2 of 3 · Optional'),
        const Text(
            'Need markers or printouts? Add them and staff will set them aside.',
            style: TextStyle(fontSize: 14)),
        if (consumables == null)
          const Tile(children: [
            Text(
                'Item selection will be available when the consumables service is connected.'),
          ])
        else ...[
          ...ConsumablePickerList.children(consumables),
          Align(
            alignment: Alignment.centerLeft,
            child: ShLink('Search all items', onPressed: _openPicker),
          ),
        ],
        Container(
          padding: const EdgeInsets.only(top: 12),
          decoration: const BoxDecoration(
              border: Border(top: BorderSide(color: AppColors.divider))),
          child: Column(
            children: [
              Kv('Items subtotal', rupees(total)),
              const SizedBox(height: 10),
              PrimaryButton('Next: review', onPressed: _next),
              GhostButton('Skip, I need no items', onPressed: () {
                consumables?.clearSelection();
                setState(() => _step = 2);
              }),
            ],
          ),
        ),
      ],
    );
  }

  /// M-06 "Book a room — step 3": check and send.
  Widget _buildReview() {
    final consumables = context.watch<ConsumablesProvider?>();
    final selected = consumables?.selection.entries.toList() ?? const [];

    return ScreenBody(
      key: const ValueKey('request-step-3'),
      children: [
        const StepperBar(step: 2),
        const Lbl('Step 3 of 3 · Check and send'),
        Tile(
          children: [
            Kv.both(
              leading: const Lbl('Purpose'),
              trailing:
                  ShLink('Edit', onPressed: () => setState(() => _step = 0)),
            ),
            Text(_objective.text),
          ],
        ),
        Tile(
          children: [
            Kv('People', '$_people'),
            Kv('Date', _longDate(_date)),
            Kv('Time',
                '${_timeFrom.format(context)} – ${_timeTo.format(context)}'),
            Kv('Your budget',
                'Rs. ${double.parse(_budget.text).toStringAsFixed(0)}'),
          ],
        ),
        Tile(
          children: [
            Kv.both(
              leading: const Lbl('Items'),
              trailing:
                  ShLink('Edit', onPressed: () => setState(() => _step = 1)),
            ),
            if (selected.isEmpty)
              const FNote('No items selected')
            else
              for (final line in selected)
                Kv(
                  '${consumables!.itemFor(line.key)?.name ?? 'Item'} × ${line.value}',
                  rupees((consumables.itemFor(line.key)?.unitPrice ?? 0) *
                      line.value),
                ),
          ],
        ),
        const FNote(
            'We will find a free room, price it and send it to the librarian for approval. You will get an email when they decide.'),
        if (_error != null) InlineError(_error!),
        PrimaryButton(
          _submitting
              ? 'Sending…'
              : widget.editing != null
                  ? 'Send again'
                  : 'Send request',
          onPressed: _submitting ? null : _submit,
        ),
      ],
    );
  }
}

/// An .input-shaped box that opens a date or time picker instead of a keyboard.
class _PickerBox extends StatelessWidget {
  final String value;
  final VoidCallback onTap;

  const _PickerBox({required this.value, required this.onTap});

  @override
  Widget build(BuildContext context) => InkWell(
        onTap: onTap,
        child: Container(
          height: 48,
          alignment: Alignment.centerLeft,
          padding: const EdgeInsets.symmetric(horizontal: 12),
          decoration: BoxDecoration(
            color: AppColors.surface,
            border: Border.all(color: AppColors.divider),
            borderRadius: AppRadius.md,
          ),
          child: Text(value, style: const TextStyle(fontSize: 14)),
        ),
      );
}
