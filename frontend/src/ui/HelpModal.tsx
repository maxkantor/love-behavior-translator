import React from 'react';

type HelpModalProps = {
  isOpen: boolean;
  onClose: () => void;
};

export function HelpModal({ isOpen, onClose }: HelpModalProps) {
  if (!isOpen) return null;

  const faqs = [
    {
      question: "How does the AI analysis work?",
      answer: "Our AI analyzes the relationship behavior you describe and provides evidence-based insights, emotional understanding, practical advice, and gentle reassurance. It's designed to help you understand patterns and communicate better."
    },
    {
      question: "What analysis styles are available?",
      answer: "We offer four analysis styles: Gentle (compassionate and supportive), Analytical (fact-based and structured), Brutally Honest (direct and unfiltered), and Light & Funny (uplifting with humor)."
    },
    {
      question: "How do credits work?",
      answer: "Each behavior analysis costs 1 credit. New users receive 5 free credits to get started. You can purchase additional credits anytime. Credits never expire."
    },
    {
      question: "Is this professional therapy?",
      answer: "No. This app provides general relationship insights and is not professional therapy or counseling. For serious relationship issues, please consult a licensed therapist or counselor."
    },
    {
      question: "Can I get a refund?",
      answer: "If you're not satisfied with your analysis, please contact support. We offer refunds for unused credits within 30 days of purchase."
    },
    {
      question: "How do I contact support?",
      answer: "You can reach our support team by clicking the 'Need Help?' button or emailing support@lovebehaviortranslator.com. We typically respond within 24 hours."
    }
  ];

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content help-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>❓ Need Help?</h2>
          <button className="modal-close" onClick={onClose}>×</button>
        </div>
        
        <div className="modal-body">
          <div className="help-sections">
            <section className="help-section">
              <h3>Frequently Asked Questions</h3>
              <div className="faq-list">
                {faqs.map((faq, index) => (
                  <div key={index} className="faq-item">
                    <div className="faq-question">{faq.question}</div>
                    <div className="faq-answer">{faq.answer}</div>
                  </div>
                ))}
              </div>
            </section>

            <section className="help-section">
              <h3>Contact Support</h3>
              <div className="contact-info">
                <p>📧 <strong>Email:</strong> support@lovebehaviortranslator.com</p>
                <p>⏰ <strong>Response time:</strong> Within 24 hours</p>
                <p>💬 <strong>Support hours:</strong> Monday - Friday, 9 AM - 5 PM EST</p>
              </div>
            </section>

            <section className="help-section">
              <h3>Quick Tips</h3>
              <ul className="tips-list">
                <li>Be specific when describing behavior - include context and examples</li>
                <li>Try different analysis styles to get varied perspectives</li>
                <li>Use the quick action buttons for common scenarios</li>
                <li>Remember: This is a tool for insight, not a replacement for professional help</li>
              </ul>
            </section>
          </div>
        </div>
      </div>
    </div>
  );
}

