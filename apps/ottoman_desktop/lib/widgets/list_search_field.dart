import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// The search box above an admin list: the server filters, so a search runs on Enter (and clearing resets it).
class ListSearchField extends StatefulWidget {
  const ListSearchField({
    super.key,
    required this.hint,
    required this.onSearch,
    this.fieldKey,
    this.width = 360,
  });

  final String hint;
  final ValueChanged<String> onSearch;
  final Key? fieldKey;
  final double width;

  @override
  State<ListSearchField> createState() => _ListSearchFieldState();
}

class _ListSearchFieldState extends State<ListSearchField> {
  final _text = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  void _run(String value) {
    setState(() => _query = value.trim());
    widget.onSearch(_query);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return SizedBox(
      width: widget.width,
      child: TextField(
        key: widget.fieldKey,
        controller: _text,
        decoration: InputDecoration(
          hintText: widget.hint,
          prefixIcon: const Icon(Icons.search),
          suffixIcon: _query.isEmpty
              ? null
              : IconButton(
                  tooltip: t.clearSearch,
                  icon: const Icon(Icons.close),
                  onPressed: () {
                    _text.clear();
                    _run('');
                  },
                ),
        ),
        textInputAction: TextInputAction.search,
        onSubmitted: _run,
      ),
    );
  }
}
