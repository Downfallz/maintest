import { test } from 'node:test';
import assert from 'node:assert/strict';
import { NOTHING_TO_RECORD, TAPPED, commentIsOpen, commentNote, noted, tappedNote } from './notes.js';

test('the one-tap notes are a lookup and a misplay, in that order', () => {
  assert.deepEqual(TAPPED.map(button => button.kind), ['Lookup', 'Misplay']);
  assert.equal(TAPPED[0].label, 'I had to look this up');
});

test('a tapped note carries no text, because the host stamps where it happened', () => {
  assert.deepEqual(tappedNote('Lookup'), { kind: 'Lookup', text: '' });
  assert.deepEqual(tappedNote('Misplay'), { kind: 'Misplay', text: '' });
});

// The two the host owns. The page cannot post one even by being asked to: a page that could write a Decision
// could rewrite how long a player took over it.
test('the page cannot write a kind the host owns', () => {
  assert.equal(tappedNote('Decision'), null);
  assert.equal(tappedNote('Refused'), null);
  assert.equal(tappedNote('Comment'), null);
  assert.equal(tappedNote(undefined), null);
});

test('a comment is trimmed, and a blank one is nothing to record', () => {
  assert.deepEqual(commentNote('  the reveal order confused us '), { kind: 'Comment', text: 'the reveal order confused us' });
  assert.equal(commentNote('   '), null);
  assert.equal(commentNote(''), null);
  assert.equal(commentNote(undefined), null);
});

// Two taps in a row are indistinguishable on a phone unless the line changes.
test('a note that landed says which kind landed', () => {
  assert.equal(noted('Lookup'), 'Noted: you looked a rule up.');
  assert.equal(noted('Misplay'), 'Noted: a misplay.');
  assert.equal(noted('Comment'), 'Comment saved with the session.');
  assert.equal(noted('Anything'), 'Noted.');
});

// Saving an empty box after saving a real comment must not leave the previous line up: that reads as a
// confirmation of something the player did not write.
test('a blank box has its own line, rather than keeping the last one that landed', () => {
  assert.notEqual(NOTHING_TO_RECORD, noted('Comment'));
  assert.match(NOTHING_TO_RECORD, /nothing/i);
});

test('the comment box belongs to the end screen and nowhere else', () => {
  assert.equal(commentIsOpen({ over: true }), true);
  assert.equal(commentIsOpen({ over: false }), false);
  assert.equal(commentIsOpen(undefined), false);
});
