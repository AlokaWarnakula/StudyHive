import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../data/demo_seed.dart';
import '../state/auth_provider.dart';
import '../state/booking_requests_provider.dart';
import '../state/profile_provider.dart';
import '../widgets/studyhive_ui.dart';
import 'consumables/browse_consumables_screen.dart';
import 'quotation/booking_history_screen.dart';

/// M-16 "Profile" — GET /api/student-profiles/{id}. Limits are read-only here;
/// an admin edits them. Students who have not onboarded yet get the profile
/// form instead (S1 self-onboarding).
class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance
        .addPostFrameCallback((_) => context.read<ProfileProvider>().refresh());
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<ProfileProvider>(
      builder: (context, provider, _) {
        if (provider.loading && !provider.loaded) {
          return const Center(child: CircularProgressIndicator());
        }
        if (provider.error != null && !provider.loaded) {
          return Padding(
            padding: const EdgeInsets.all(16),
            child: InlineError(provider.error!),
          );
        }
        return provider.profile == null
            ? const _OnboardingForm()
            : const _ProfileView();
      },
    );
  }
}

class _ProfileView extends StatelessWidget {
  const _ProfileView();

  @override
  Widget build(BuildContext context) {
    final profile = context.watch<ProfileProvider>().profile!;
    final auth = context.watch<AuthProvider>();
    final requests = context.watch<BookingRequestsProvider>().requests;
    final used = requests
        .where((r) => !{'Draft', 'Rejected', 'Completed', 'Cancelled', 'Failed'}
            .contains(r.status))
        .length;

    return ScreenBody(
      children: [
        Row(
          children: [
            const Ph(label: 'avatar', width: 64, height: 64),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(auth.studentName ?? 'Student profile',
                      style: const TextStyle(
                          fontSize: 19, fontWeight: FontWeight.w600)),
                  if (auth.studentEmail != null) FNote(auth.studentEmail!),
                  FNote(
                      '${profile.department} · Year ${profile.yearOfStudy}'),
                ],
              ),
            ),
          ],
        ),
        Tile(
          children: [
            Kv('Student number', profile.studentNumber),
            Kv.widget(
              label: 'Account',
              trailing: ShTag.forStatus(profile.isActive ? 'Active' : 'Inactive'),
            ),
            Kv('Bookings this week', '$used of ${profile.maxBookingsPerWeek}'),
            Kv(
                'Outstanding penalties',
                profile.penaltyPoints == 0
                    ? 'None'
                    : '${profile.penaltyPoints} points'),
            if (profile.suspendedUntil != null)
              Kv('Suspended until', profile.suspendedUntil!),
          ],
        ),
        Tile(
          children: [
            const Lbl('Spend'),
            if (demoPreviewEnabled) ...[
              const DemoPreviewBanner(),
              const Kv('This month', 'Rs. 1,020'),
              const Kv('All bookings', 'Rs. 4,380'),
            ] else
              const FNote(
                  'Spend totals appear when the booking history API is connected.'),
            ShLink('See past bookings and costs',
                onPressed: () => Navigator.of(context).push(MaterialPageRoute(
                    builder: (_) => const BookingHistoryScreen()))),
          ],
        ),
        Column(
          children: [
            _SettingRow(
                label: 'Browse consumables',
                onTap: () => Navigator.of(context).push(MaterialPageRoute(
                    builder: (_) => const BrowseConsumablesScreen()))),
            const SizedBox(height: 2),
            const _SettingRow(label: 'Notifications'),
            const SizedBox(height: 2),
            const _SettingRow(label: 'Change password'),
            const SizedBox(height: 2),
            const _SettingRow(label: 'Help'),
          ],
        ),
        SecondaryButton(
          'Sign out',
          onPressed: () {
            context.read<ProfileProvider>().reset();
            context.read<BookingRequestsProvider>().reset();
            context.read<AuthProvider>().logout();
          },
        ),
      ],
    );
  }
}

/// The settings rows at the bottom of M-16 — a .tile per row with a chevron.
class _SettingRow extends StatelessWidget {
  final String label;
  final VoidCallback? onTap;

  const _SettingRow({required this.label, this.onTap});

  @override
  Widget build(BuildContext context) => Tile.row(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 16),
        onTap: onTap,
        children: [
          Expanded(child: Text(label, style: const TextStyle(fontSize: 15))),
          const Icon(Icons.chevron_right, size: 20),
        ],
      );
}

class _OnboardingForm extends StatefulWidget {
  const _OnboardingForm();

  @override
  State<_OnboardingForm> createState() => _OnboardingFormState();
}

class _OnboardingFormState extends State<_OnboardingForm> {
  final _formKey = GlobalKey<FormState>();
  final _studentNumberController = TextEditingController();
  final _departmentController = TextEditingController();
  final _yearController = TextEditingController(text: '1');
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _studentNumberController.dispose();
    _departmentController.dispose();
    _yearController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _error = null;
      _submitting = true;
    });
    try {
      await context.read<ProfileProvider>().onboard(
            studentNumber: _studentNumberController.text.trim(),
            department: _departmentController.text.trim(),
            yearOfStudy: int.parse(_yearController.text),
          );
    } on ApiException catch (e) {
      setState(() => _error = e.toString());
    } catch (_) {
      setState(() => _error = 'Something went wrong. Please try again.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _formKey,
      child: ScreenBody(
        children: [
          const Heading('Finish setting up your student profile',
              fontSize: 25),
          const FNote(
              'This is required before you can create booking requests.'),
          ShTextField(
            label: 'Student number',
            controller: _studentNumberController,
            validator: (v) => (v == null || v.trim().isEmpty)
                ? 'Student number is required'
                : null,
          ),
          ShTextField(
            label: 'Department',
            controller: _departmentController,
            validator: (v) =>
                (v == null || v.trim().isEmpty) ? 'Department is required' : null,
          ),
          ShTextField(
            label: 'Year of study',
            controller: _yearController,
            keyboardType: TextInputType.number,
            validator: (v) {
              final n = int.tryParse(v ?? '');
              if (n == null || n < 1 || n > 5) {
                return 'Enter a year between 1 and 5';
              }
              return null;
            },
          ),
          if (_error != null) InlineError(_error!),
          PrimaryButton(
            _submitting ? 'Saving…' : 'Save profile',
            onPressed: _submitting ? null : _submit,
          ),
        ],
      ),
    );
  }
}
