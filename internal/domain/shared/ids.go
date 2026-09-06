package shared

import "github.com/google/uuid"

// NewID génère un identifiant UUID v4.
func NewID() string {
	return uuid.NewString()
}

// IsValidID vérifie qu'une chaîne est un UUID valide.
func IsValidID(id string) bool {
	_, err := uuid.Parse(id)
	return err == nil
}
