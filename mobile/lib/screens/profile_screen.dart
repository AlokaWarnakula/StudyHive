import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/auth_provider.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(auth.studentName ?? 'Student profile'),
          const SizedBox(height: 16),
          OutlinedButton(
            onPressed: () => context.read<AuthProvider>().logout(),
            child: const Text('Sign out'),
          ),
        ],
      ),
    );
  }
}
