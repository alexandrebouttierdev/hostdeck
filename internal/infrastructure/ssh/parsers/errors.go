package parsers

import "errors"

var (
	// ErrEmptyInput is returned when the input string is empty or whitespace-only.
	ErrEmptyInput = errors.New("parsers: empty input")
	// ErrInvalidFormat is returned when the input cannot be parsed.
	ErrInvalidFormat = errors.New("parsers: invalid format")
	// ErrMissingField is returned when a required field is absent.
	ErrMissingField = errors.New("parsers: missing field")
	// ErrInvalidValue is returned when a field value is not a valid number.
	ErrInvalidValue = errors.New("parsers: invalid value")
)

// ParseError describes a parsing failure with optional field context.
type ParseError struct {
	Op    string
	Field string
	Err   error
}

func (e *ParseError) Error() string {
	if e.Field != "" {
		return e.Op + ": " + e.Field + ": " + e.Err.Error()
	}
	return e.Op + ": " + e.Err.Error()
}

func (e *ParseError) Unwrap() error {
	return e.Err
}

func newParseError(op, field string, err error) *ParseError {
	return &ParseError{Op: op, Field: field, Err: err}
}
