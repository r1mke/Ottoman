import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';

/// Title row for dialogs: the title on the left and a close button ("X") in the top right corner, as users expect.
/// Use as `AlertDialog(title: DialogHeader(title))`.
class DialogHeader extends StatelessWidget {
  const DialogHeader(this.title, {super.key, this.onClose});

  final String title;

  /// Defaults to popping the dialog without a result.
  final VoidCallback? onClose;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Row(
      children: [
        Expanded(child: Text(title)),
        IconButton(
          key: const Key('dialog_close'),
          tooltip: t.close,
          icon: const Icon(Icons.close),
          onPressed: onClose ?? () => Navigator.maybePop(context),
        ),
      ],
    );
  }
}
