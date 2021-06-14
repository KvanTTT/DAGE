import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:antlr4/src/interval_set.dart';
import 'package:antlr4/src/token.dart';

import 'package:antlr4/antlr4.dart';

class AntlrCaseInsensitiveInputStream extends CharStream {
  final name = '<empty>';
  List<int> data;
  List<int> normalizedData;
  int _index = 0;
  bool decodeToUnicodeCodePoints = false;

  AntlrCaseInsensitiveInputStream.fromString(String data, bool lowerCase) {
    this.data = data.runes.toList(growable: false);
    this.normalizedData = (lowerCase ? data.toLowerCase() : data.toUpperCase())
        .runes
        .toList(growable: false);
  }

  static Future<AntlrCaseInsensitiveInputStream> fromStringStream(
      Stream<String> stream, bool lowerCase) async {
    final data = StringBuffer();
    await stream.listen((buf) {
      data.write(buf);
    }).asFuture();
    return AntlrCaseInsensitiveInputStream.fromString(
        data.toString(), lowerCase);
  }

  static Future<AntlrCaseInsensitiveInputStream> fromStream(
      Stream<List<int>> stream, bool lowerCase,
      {Encoding encoding = utf8}) {
    final data = stream.transform(encoding.decoder);
    return fromStringStream(data, lowerCase);
  }

  static Future<AntlrCaseInsensitiveInputStream> fromPath(
      String path, bool lowerCase,
      {Encoding encoding = utf8}) {
    return fromStream(File(path).openRead(), lowerCase);
  }

  @override
  int get index {
    return _index;
  }

  @override
  int get size {
    return data.length;
  }

  /// Reset the stream so that it's in the same state it was
  /// when the object was created *except* the data array is not
  /// touched.
  void reset() {
    _index = 0;
  }

  @override
  void consume() {
    if (_index >= size) {
      // assert this.LA(1) == Token.EOF
      throw ('cannot consume EOF');
    }
    _index += 1;
  }

  @override
  int LA(int offset) {
    if (offset == 0) {
      return 0; // undefined
    }
    if (offset < 0) {
      offset += 1; // e.g., translate LA(-1) to use offset=0
    }
    final pos = _index + offset - 1;
    if (pos < 0 || pos >= size) {
      // invalid
      return Token.EOF;
    }
    return normalizedData[pos];
  }

  /// mark/release do nothing; we have entire buffer
  @override
  int mark() {
    return -1;
  }

  @override
  void release(int marker) {}

  /// consume() ahead until p==_index; can't just set p=_index as we must
  /// update line and column. If we seek backwards, just set p
  @override
  void seek(int _index) {
    if (_index <= this._index) {
      this._index = _index; // just jump; don't update stream state (line,
      // ...)
      return;
    }
    // seek forward
    this._index = min(_index, size);
  }

  @override
  String getText(Interval interval) {
    final startIdx = min(interval.a, size);
    final len = min(interval.b - interval.a + 1, size - startIdx);
    return String.fromCharCodes(data, startIdx, startIdx + len);
  }

  @override
  String toString() {
    return String.fromCharCodes(data);
  }

  @override
  String get sourceName {
    // TODO: implement getSourceName
    return IntStream.UNKNOWN_SOURCE_NAME;
  }
}
