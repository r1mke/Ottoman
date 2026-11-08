import 'dart:io';

import 'package:ottoman_core/ottoman_core.dart';
import 'package:printing/printing.dart';
import 'package:path_provider/path_provider.dart';

/// Where an exported report goes. A seam so screens can be tested without touching the disk.
abstract class FileSaver {
  /// Writes the file and returns the full path it was saved to.
  Future<String> save(DownloadedFile file);

  /// Shows the saved file in the system file manager.
  Future<void> reveal(String path);

  /// Sends a PDF to a printer through the system print dialog. False when the person closed the dialog without
  /// printing; throws when printing could not be started at all.
  Future<bool> printPdf(DownloadedFile file);
}

/// Saves into the user's Downloads folder and never overwrites: "izvjestaj (1).csv" if the name is taken.
class DownloadsFileSaver implements FileSaver {
  const DownloadsFileSaver();

  @override
  Future<String> save(DownloadedFile file) async {
    final dir = await getDownloadsDirectory() ?? Directory.systemTemp;
    final target = _free(dir, file.fileName);
    await target.writeAsBytes(file.bytes, flush: true);
    return target.path;
  }

  static File _free(Directory dir, String name) {
    final dot = name.lastIndexOf('.');
    final stem = dot > 0 ? name.substring(0, dot) : name;
    final ext = dot > 0 ? name.substring(dot) : '';
    var candidate = File('${dir.path}${Platform.pathSeparator}$name');
    for (var i = 1; candidate.existsSync(); i++) {
      candidate = File('${dir.path}${Platform.pathSeparator}$stem ($i)$ext');
    }
    return candidate;
  }

  @override
  Future<bool> printPdf(DownloadedFile file) => Printing.layoutPdf(
    name: file.fileName,
    onLayout: (_) async => file.bytes,
  );

  @override
  Future<void> reveal(String path) async {
    if (Platform.isWindows) {
      await Process.run('explorer', ['/select,', path]);
    } else if (Platform.isMacOS) {
      await Process.run('open', ['-R', path]);
    } else {
      await Process.run('xdg-open', [File(path).parent.path]);
    }
  }
}
