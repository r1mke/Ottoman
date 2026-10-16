import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

class ChosenImage {
  const ChosenImage(this.name, this.bytes);

  final String name;
  final Uint8List bytes;
}

/// Where a picture comes from. A seam so dialogs can be tested without a native file dialog.
abstract class ImageChooser {
  Future<ChosenImage?> choose();
}

class FileImageChooser implements ImageChooser {
  const FileImageChooser();

  @override
  Future<ChosenImage?> choose() async {
    final file = await FilePicker.pickFile(type: FileType.image);
    if (file == null) return null;
    return ChosenImage(file.name, await file.readAsBytes());
  }
}

class EditablePicture {
  const EditablePicture(this.id, this.url);

  final Object id;
  final String url;
}

/// Thumbnails with remove buttons and an add (or change, when only one is allowed) button.
/// Changes go to the server immediately: the picture is not part of the Save of the surrounding form.
/// The server decides what is acceptable (type from the bytes, size), its message is shown as is.
class PictureEditor extends StatefulWidget {
  const PictureEditor({
    super.key,
    required this.pictures,
    required this.onAdd,
    required this.onRemove,
    this.chooser = const FileImageChooser(),
    this.maxCount = 1,
    this.fallbackIcon = Icons.image_outlined,
  });

  final List<EditablePicture> pictures;

  /// Uploads the picture and returns the complete list as the server now has it.
  final Future<List<EditablePicture>> Function(ChosenImage image) onAdd;

  /// Removes the picture and returns the complete list as the server now has it.
  final Future<List<EditablePicture>> Function(EditablePicture picture)
  onRemove;
  final ImageChooser chooser;
  final int maxCount;
  final IconData fallbackIcon;

  @override
  State<PictureEditor> createState() => _PictureEditorState();
}

class _PictureEditorState extends State<PictureEditor> {
  late List<EditablePicture> _pictures = [...widget.pictures];
  bool _busy = false;
  String? _error;

  Future<void> _run(Future<List<EditablePicture>> Function() action) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final next = await action();
      if (mounted) setState(() => _pictures = next);
    } on AppException catch (e) {
      final detail = e.errors.values.expand((x) => x).join('\n');
      if (mounted) setState(() => _error = detail.isEmpty ? e.message : detail);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _add() async {
    final image = await widget.chooser.choose();
    if (image == null) return;
    await _run(() => widget.onAdd(image));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final single = widget.maxCount == 1;
    final canAdd = single || _pictures.length < widget.maxCount;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(t.pictures, style: Theme.of(context).textTheme.labelLarge),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            for (final p in _pictures)
              Stack(
                key: Key('picture_${p.id}'),
                children: [
                  ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: NetworkPicture(
                      url: p.url,
                      width: 84,
                      height: 84,
                      fallbackIcon: widget.fallbackIcon,
                    ),
                  ),
                  Positioned(
                    top: 0,
                    right: 0,
                    child: IconButton.filledTonal(
                      key: Key('remove_picture_${p.id}'),
                      tooltip: t.removePicture,
                      visualDensity: VisualDensity.compact,
                      iconSize: 16,
                      onPressed: _busy
                          ? null
                          : () => _run(() => widget.onRemove(p)),
                      icon: const Icon(Icons.close),
                    ),
                  ),
                ],
              ),
            if (canAdd)
              OutlinedButton.icon(
                key: const Key('add_picture'),
                style: OutlinedButton.styleFrom(minimumSize: const Size(0, 44)),
                onPressed: _busy ? null : _add,
                icon: _busy
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.add_photo_alternate_outlined),
                label: Text(
                  single && _pictures.isNotEmpty
                      ? t.changePicture
                      : t.addPicture,
                ),
              ),
          ],
        ),
        if (_error != null)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              _error!,
              key: const Key('picture_error'),
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ),
      ],
    );
  }
}
