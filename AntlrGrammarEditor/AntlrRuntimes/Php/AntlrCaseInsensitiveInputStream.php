<?php

use Antlr\Antlr4\Runtime\CharStream;
use Antlr\Antlr4\Runtime\Token;
use Antlr\Antlr4\Runtime\Utils\StringUtils;

class AntlrCaseInsensitiveInputStream implements CharStream
{
    /** @var int */
    protected $index = 0;

    /** @var int */
    protected $size = 0;

    /** @var string */
    public $name = '<empty>';

    /** @var string */
    public $input;

    /** @var array<int> */
    public $characters = [];

    /**
     * @param array<string> $characters
     */
    private function __construct(string $input, array $characters)
    {
        $this->input = $input;
        $this->characters = $characters;
        $this->size = \count($this->characters);
    }

    public static function fromPath(string $path, bool $lowerCase) : AntlrCaseInsensitiveInputStream
    {
        $content = file_get_contents($path);

        if ($content === false) {
            throw new \InvalidArgumentException(\sprintf('File not found at %s.', $path));
        }

        return self::fromString($content, $lowerCase);
    }

    public static function fromString(string $input, bool $lowerCase) : AntlrCaseInsensitiveInputStream
    {
        $chars = mb_str_split($input);
        $result = array();
        foreach ($chars as $char) {
            $normalized_char = $lowerCase ? mb_strtolower($char) : mb_strtoupper($char);
            $result_char = mb_strlen($normalized_char) > 1 ? $char : $normalized_char;
            $result[] =  StringUtils::codePoint($result_char);
        }

        return new self($input, $result);
    }

    public function getIndex() : int
    {
        return $this->index;
    }

    public function getLength() : int
    {
        return $this->size;
    }

    public function consume() : void
    {
        if ($this->index >= $this->size) {
            // assert this.LA(1) == Token.EOF
            throw new \RuntimeException('Cannot consume EOF.');
        }

        $this->index++;
    }

    public function LA(int $offset) : int
    {
        if ($offset === 0) {
            return 0;// undefined
        }

        if ($offset < 0) {
            // e.g., translate LA(-1) to use offset=0
            $offset++;
        }

        $pos = $this->index + $offset - 1;

        if ($pos < 0 || $pos >= $this->size) {
            // invalid
            return Token::EOF;
        }

        return $this->characters[$pos];
    }

    public function LT(int $offset) : int
    {
        return $this->LA($offset);
    }

    /**
     * Mark/release do nothing; we have entire buffer
     */
    public function mark() : int
    {
        return -1;
    }

    public function release(int $marker) : void
    {
    }

    /**
     * {@see self::consume()} ahead until `$p === $this->index`; Can't just set
     * `$p = $this->index` as we must update line and column. If we seek
     * backwards, just set `$p`.
     */
    public function seek(int $index) : void
    {
        if ($index <= $this->index) {
            $this->index = $index; // just jump; don't update stream state (line, ...)

            return;
        }

        // seek forward
        $this->index = \min($index, $this->size);
    }

    public function getText(int $start, int $stop) : string
    {
        if ($stop >= $this->size) {
            $stop = $this->size - 1;
        }

        if ($start >= $this->size) {
            return '';
        }

        return substr($this->input, $start, $stop - $start + 1); // Is this correct?
    }

    public function getSourceName() : string
    {
        return '';
    }

    public function __toString() : string
    {
        return $this->input;
    }
}