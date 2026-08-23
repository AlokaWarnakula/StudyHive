import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../data/demo_seed.dart';
import '../models/booking_request.dart';
import '../models/consumable.dart';
import '../state/booking_requests_provider.dart';
import '../theme/app_theme.dart';
import '../widgets/studyhive_ui.dart';
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
class CreateRequestScreen extends StatefulWidget {
  const CreateRequestScreen({super.key});

  @override
  State<CreateRequestScreen> createState() => _CreateRequestScreenState();
}

class _CreateRequestScreenState extends State<CreateRequestScreen> {
  final _formKey = GlobalKey<FormState>();
  final _objective = TextEditingController();
  final _budget = TextEditingController(text: '1000');
  final Map<String, int> _quantities = {};
  int _step = 0;
  int _people = 4;
  DateTime _date = DateTime.now().add(const Duration(days: 3));
  TimeOfDay _timeFrom = const TimeOfDay(hour: 14, minute: 0);
  TimeOfDay _timeTo = const TimeOfDay(hour: 16, minute: 0);
  String? _error;
  bool _submitting = false;

  @override
  void dispose() {
    _objective.dispose();
    _budget.dispose();
    super.dispose();
  }

  List<ConsumableDetail> get _catalogue =>
      demoPreviewEnabled ? demoConsumables : const <ConsumableDetail>[];

  Future<void> _pickDate() async {
    final selected = await showDatePicker(
      context: context,
      initialDate: _date,
      firstDate: DateTime.now(),
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
    setState(() => _step += 1);
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
    final items = _catalogue
        .where((item) => (_quantities[item.id] ?? 0) > 0)
        .map((item) => BookingRequestItem(
            consumableId: item.id, quantity: _quantities[item.id]!))
        .toList();
    try {
      final created =
          await context.read<BookingRequestsProvider>().createAndSubmit(
                objective: _objective.text.trim(),
                groupSize: _people,
                preferredDateFrom: _isoDate(_date),
                preferredDateTo: _isoDate(_date),
                preferredTimeFrom: _isoTime(_timeFrom),
                preferredTimeTo: _isoTime(_timeTo),
                sessionsRequired: 1,
                sessionDurationMinutes: _durationMinutes,
                budget: double.parse(_budget.text),
                items: items,
              );
      if (!mounted) return;
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(
            builder: (_) => WorkflowProgressScreen(requestId: created.id)),
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

  double get _itemsTotal => _catalogue.fold(
        0,
        (total, item) => total + item.unitPrice * (_quantities[item.id] ?? 0),
      );

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
          _ => 'Book a room',
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

  /// M-05 "Book a room — step 2": the optional consumables picker.
  Widget _buildItems() {
    return ScreenBody(
      key: const ValueKey('request-step-2'),
      children: [
        const StepperBar(step: 1),
        const Lbl('Step 2 of 3 · Optional'),
        const Text(
            'Need markers or printouts? Add them and staff will set them aside.',
            style: TextStyle(fontSize: 14)),
        if (!demoPreviewEnabled)
          const Tile(children: [
            Text(
                'Item selection will be available when the consumables service is connected.'),
          ])
        else ...[
          const DemoPreviewBanner(),
          for (final item in demoConsumables) _itemPicker(item),
        ],
        Container(
          padding: const EdgeInsets.only(top: 12),
          decoration: const BoxDecoration(
              border: Border(top: BorderSide(color: AppColors.divider))),
          child: Column(
            children: [
              Kv('Items subtotal', 'Rs. ${_itemsTotal.toStringAsFixed(0)}'),
              const SizedBox(height: 10),
              PrimaryButton('Next: review', onPressed: _next),
              GhostButton('Skip, I need no items',
                  onPressed: () => setState(() {
                        _quantities.clear();
                        _step = 2;
                      })),
            ],
          ),
        ),
      ],
    );
  }

  Widget _itemPicker(ConsumableDetail item) {
    final quantity = _quantities[item.id] ?? 0;
    final inStock = item.availableQuantity > 0;

    if (!inStock) {
      return Tile(
        gap: 10,
        opacity: 0.55,
        children: [
          Kv.widget(
            label: item.name,
            trailing: const ShTag('Out of stock', tone: TagTone.neutral),
          ),
          FNote(item.description ?? 'Staff will restock this item soon.'),
        ],
      );
    }

    return Tile(
      gap: 10,
      children: [
        Kv(item.name, 'Rs. ${item.unitPrice.toStringAsFixed(0)}'),
        Kv.both(
          leading: FNote('${item.availableQuantity} in stock'),
          trailing: CounterControl(
            value: quantity,
            max: item.availableQuantity,
            size: 44,
            valueFontSize: 17,
            onChanged: (value) =>
                setState(() => _quantities[item.id] = value),
          ),
        ),
      ],
    );
  }

  /// M-06 "Book a room — step 3": check and send.
  Widget _buildReview() {
    final selected = _catalogue
        .where((item) => (_quantities[item.id] ?? 0) > 0)
        .toList();

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
              for (final item in selected)
                Kv('${item.name} × ${_quantities[item.id]}',
                    'Rs. ${(item.unitPrice * _quantities[item.id]!).toStringAsFixed(0)}'),
          ],
        ),
        const FNote(
            'We will find a free room, price it and send it to the librarian for approval. You will get a notification.'),
        if (_error != null) InlineError(_error!),
        PrimaryButton(
          _submitting ? 'Sending…' : 'Send request',
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
