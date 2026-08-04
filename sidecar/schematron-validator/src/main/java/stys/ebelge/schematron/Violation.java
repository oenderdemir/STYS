package stys.ebelge.schematron;

/** Tek bir Schematron ihlalini taşır. Mesaj GİB kural setinin kendi (Türkçe) metnidir. */
record Violation(String ruleId, String location, String message, String severity) {
}
