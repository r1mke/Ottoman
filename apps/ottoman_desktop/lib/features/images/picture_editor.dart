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

  static const _maxBytes = 5 * 1024 * 1024;
  static final _extensions = RegExp(
    r'\.(jpe?g|png|webp)$',
    caseSensitive: false,
  );

  Future<void> _add() async {
    final messenger = ScaffoldMessenger.of(context);
    final hint = AppLocalizations.of(context).pictureHint;
    final image = await widget.chooser.choose();
    if (image == null) return;
    if ((image.bytes.length > _maxBytes || !_extensions.hasMatch(image.name))) {
      messenger.showSnackBar(SnackBar(content: Text(hint)));
      return;
    }
    await _run(() => widget.onAdd(image));
    if (_error != null) {
      messenger.showSnackBar(SnackBar(content: Text('$_error\n$hint')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final single = widget.maxCount == 1;
    final canAdd = single || _pictures.length < widget.maxCount;
    final size = single ? 148.0 : 108.0;
    final replaceInPlace = single && _pictures.isNotEmpty;
    // Full width, so the content is centred in the form whatever the parent aligns to.
    return SizedBox(
      width: double.infinity,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          // A single picture needs no heading; several get a count.
          if (!single) ...[
            Text(
              '${_pictures.length} / ${widget.maxCount}',
              key: const Key('picture_count'),
              style: const TextStyle(
                color: WorkspaceColors.muted,
                fontSize: 12,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 10),
          ],
          Wrap(
            spacing: 12,
            runSpacing: 12,
            alignment: WrapAlignment.center,
            children: [
              for (var i = 0; i < _pictures.length; i++)
                _PictureTile(
                  key: Key('picture_${_pictures[i].id}'),
                  picture: _pictures[i],
                  size: size,
                  isMain: !single && i == 0,
                  fallbackIcon: widget.fallbackIcon,
                  removeTooltip: t.removePicture,
                  removeKey: Key('remove_picture_${_pictures[i].id}'),
                  onRemove: _busy
                      ? null
                      : () => _run(() => widget.onRemove(_pictures[i])),
                ),
              if (canAdd && !replaceInPlace)
                _AddTile(
                  key: const Key('add_picture'),
                  size: size,
                  label: t.addPicture,
                  busy: _busy,
                  onTap: _busy ? null : _add,
                ),
            ],
          ),
          if (replaceInPlace)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: TextButton(
                key: const Key('add_picture'),
                onPressed: _busy ? null : _add,
                child: _busy
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(t.changePicture),
              ),
            ),
        ],
      ),
    );
  }
}

/// One uploaded picture: a rounded tile with a small remove button; the first of several is marked as the main one.
class _PictureTile extends StatelessWidget {
  const _PictureTile({
    super.key,
    required this.picture,
    required this.size,
    required this.isMain,
    required this.fallbackIcon,
    required this.removeTooltip,
    required this.removeKey,
    required this.onRemove,
  });

  final EditablePicture picture;
  final double size;
  final bool isMain;
  final IconData fallbackIcon;
  final String removeTooltip;
  final Key removeKey;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: WorkspaceColors.line),
        boxShadow: const [
          BoxShadow(
            color: Color(0x14000000),
            blurRadius: 8,
            offset: Offset(0, 2),
          ),
        ],
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(13),
        child: Stack(
          fit: StackFit.expand,
          children: [
            NetworkPicture(
              url: picture.url,
              width: size,
              height: size,
              fallbackIcon: fallbackIcon,
            ),
            if (isMain)
              Positioned(
                left: 6,
                bottom: 6,
                child: Container(
                  key: const Key('picture_main_badge'),
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 3,
                  ),
                  decoration: BoxDecoration(
                    color: WorkspaceColors.amber,
                    borderRadius: BorderRadius.circular(99),
                  ),
                  child: Text(
                    t.mainPicture,
                    style: const TextStyle(
                      color: WorkspaceColors.ink,
                      fontSize: 11,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
              ),
            Positioned(
              top: 6,
              right: 6,
              child: Tooltip(
                message: removeTooltip,
                child: Material(
                  color: Colors.black.withValues(alpha: 0.6),
                  shape: const CircleBorder(),
                  child: InkWell(
                    key: removeKey,
                    customBorder: const CircleBorder(),
                    onTap: onRemove,
                    child: const Padding(
                      padding: EdgeInsets.all(5),
                      child: Icon(Icons.close, size: 15, color: Colors.white),
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The "+" tile with a dashed outline that opens the file dialog.
class _AddTile extends StatelessWidget {
  const _AddTile({
    super.key,
    required this.size,
    required this.label,
    required this.busy,
    required this.onTap,
  });

  final double size;
  final String label;
  final bool busy;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final color = onTap == null
        ? WorkspaceColors.neutral
        : WorkspaceColors.muted;
    return InkWell(
      borderRadius: BorderRadius.circular(14),
      onTap: onTap,
      child: CustomPaint(
        painter: _DashedBorderPainter(color: color, radius: 14),
        child: SizedBox(
          width: size,
          height: size,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (busy)
                const SizedBox(
                  width: 26,
                  height: 26,
                  child: CircularProgressIndicator(strokeWidth: 2.5),
                )
              else
                Icon(
                  Icons.add_photo_alternate_outlined,
                  size: 30,
                  color: color,
                ),
              const SizedBox(height: 8),
              Text(
                label,
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: color,
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _DashedBorderPainter extends CustomPainter {
  const _DashedBorderPainter({required this.color, required this.radius});

  final Color color;
  final double radius;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.6;
    final path = Path()
      ..addRRect(
        RRect.fromRectAndRadius(
          Offset.zero & size,
          Radius.circular(radius),
        ).deflate(0.8),
      );
    for (final metric in path.computeMetrics()) {
      var d = 0.0;
      while (d < metric.length) {
        canvas.drawPath(metric.extractPath(d, d + 6), paint);
        d += 11;
      }
    }
  }

  @override
  bool shouldRepaint(_DashedBorderPainter old) =>
      old.color != color || old.radius != radius;
}
