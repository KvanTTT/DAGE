import antlr4 from 'antlr4';

function AntlrCaseInsensitiveInputStream(data, lowerCase) {
    antlr4.InputStream.call(this, data);
    this._lookaheadData = [];
    for (let i = 0; i < data.length; i++) {
        let char = data.charAt(i);
        let normalized_char = lowerCase ? char.toLowerCase() : char.toUpperCase();
        this._lookaheadData.push((normalized_char.length > 1 ? char : normalized_char).charCodeAt(0));
    }
    return this;
}

AntlrCaseInsensitiveInputStream.prototype = Object.create(antlr4.InputStream.prototype);
AntlrCaseInsensitiveInputStream.prototype.constructor = AntlrCaseInsensitiveInputStream;

AntlrCaseInsensitiveInputStream.prototype.LA = function (offset) {
    if (offset === 0) {
        return 0; // undefined
    }
    if (offset < 0) {
        offset += 1; // e.g., translate LA(-1) to use offset=0
    }
    let pos = this._index + offset - 1;
    if (pos < 0 || pos >= this._size) { // invalid
        return antlr4.Token.EOF;
    }
    return this._lookaheadData[pos];
};

export default AntlrCaseInsensitiveInputStream;