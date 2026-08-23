import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../state/auth_provider.dart';
import '../widgets/studyhive_ui.dart';

/// M-02 "Create account" — POST /api/auth/register, which creates the
/// student_profiles row. Year of study is a segmented control in the reference,
/// not a dropdown.
class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  static const _departments = [
    'Faculty of Computing',
    'Faculty of Engineering',
    'Faculty of Business',
  ];

  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();
  String _department = _departments.first;
  int _year = 1;
  String? _error;
  bool _submitting = false;

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _error = null;
      _submitting = true;
    });
    try {
      await context.read<AuthProvider>().register(
            fullName: _name.text.trim(),
            email: _email.text.trim(),
            department: _department,
            yearOfStudy: _year,
            password: _password.text,
          );
      if (mounted) Navigator.of(context).popUntil((route) => route.isFirst);
    } on ApiException catch (e) {
      setState(() => _error = e.toString());
    } catch (_) {
      setState(() =>
          _error = 'Account creation is unavailable. Please try again later.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Create account')),
      body: Form(
        key: _formKey,
        child: ScreenBody(
          children: [
            ShTextField(
              label: 'Full name',
              controller: _name,
              validator: (value) => value == null || value.trim().length < 2
                  ? 'Enter your full name'
                  : null,
            ),
            ShTextField(
              label: 'University email',
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              validator: (value) => value == null || !value.contains('@')
                  ? 'Enter a valid university email'
                  : null,
            ),
            Field(
              label: 'Department',
              child: DropdownButtonFormField<String>(
                initialValue: _department,
                isExpanded: true,
                decoration: const InputDecoration(
                    constraints: BoxConstraints(minHeight: 48)),
                style: Theme.of(context)
                    .textTheme
                    .bodyMedium!
                    .copyWith(fontSize: 14),
                items: [
                  for (final department in _departments)
                    DropdownMenuItem(
                        value: department, child: Text(department))
                ],
                onChanged: (value) => setState(() => _department = value!),
              ),
            ),
            Field(
              label: 'Year of study',
              child: Segmented<int>(
                options: const [(1, '1'), (2, '2'), (3, '3'), (4, '4')],
                value: _year,
                onChanged: (value) => setState(() => _year = value),
              ),
            ),
            ShTextField(
              label: 'Password',
              controller: _password,
              obscureText: true,
              validator: (value) {
                if (value == null ||
                    value.length < 8 ||
                    !RegExp(r'\d').hasMatch(value)) {
                  return 'Use at least 8 characters and one number';
                }
                return null;
              },
            ),
            const FNote('At least 8 characters, one number.'),
            if (_error != null) InlineError(_error!),
            PrimaryButton(
              _submitting ? 'Creating…' : 'Create account',
              onPressed: _submitting ? null : _submit,
            ),
          ],
        ),
      ),
    );
  }
}
